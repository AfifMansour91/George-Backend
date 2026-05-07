using George.Common;
using George.DB;
using George.Services.Response;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace George.Services
{
    /// <summary>
    /// Brand-specific methods for the WooCommerce REST integration.
    ///
    /// Important payload-shape note (see brands-feature-spec.md §5.7):
    ///   When assigning brands to a product the body is a FLAT ARRAY of IDs:
    ///       {"brands":[16,21]}
    ///   NOT an array of objects like categories. The wrapper type
    ///   <see cref="WooProductBrandsAssignmentBody"/> below pins this shape down,
    ///   and a unit test in George.Services.Tests asserts the exact serialized JSON.
    /// </summary>
    public partial class WooCommerceService
    {
        //*************************    Helpers / DTOs    *************************//

        /// <summary>The exact body shape expected by PUT /products/{id} when assigning brands.</summary>
        public sealed class WooProductBrandsAssignmentBody
        {
            [JsonPropertyName("brands")]
            public int[] Brands { get; set; } = Array.Empty<int>();
        }

        /// <summary>JSON shape returned by GET/POST/PUT /products/brands.</summary>
        internal sealed class WooBrandResponse
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? slug { get; set; }
            public string? description { get; set; }
            public int? parent { get; set; }
            public int? count { get; set; }
            public WooBrandImage? image { get; set; }
        }

        internal sealed class WooBrandImage
        {
            public int? id { get; set; }
            public string? src { get; set; }
        }

        /// <summary>POST/PUT body for /products/brands.</summary>
        private sealed class WooBrandPayload
        {
            public string? name { get; set; }
            public string? slug { get; set; }
            public string? description { get; set; }
            public int? parent { get; set; }
            public WooBrandImage? image { get; set; }
        }

        //*************************    Public methods    *************************//

        /// <summary>
        /// Lists brands from a site's WooCommerce. Pages through results 100 at a time.
        /// Returns null when WooCommerce returns 404 for /products/brands — that means the
        /// store is on a pre-9.6 WooCommerce that doesn't have Brands as a core taxonomy.
        /// Callers should fall back to the legacy `_brand` meta-key path in that case.
        ///
        /// Internal-only: step 7's public import wrapper will call this; not exposed externally.
        /// </summary>
        internal async Task<List<WooBrandResponse>?> GetBrandsForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var (httpClient, baseUrl, ok) = await TryOpenWooClientForSiteAsync(siteId, cancelToken);
            if (!ok || httpClient == null || baseUrl == null) return new List<WooBrandResponse>();

            using (httpClient)
            {
                return await GetAllBrandsAsync(baseUrl, httpClient, cancelToken);
            }
        }

        /// <summary>
        /// Creates or updates a single Brand in WooCommerce for a Woo-linked site, then writes
        /// the returned WooCommerce term id back to the local Brand row. Mirrors
        /// <see cref="SyncCategoryToWooCommerceAsync"/>.
        /// </summary>
        public async Task<IApiResponse<WooCommerceBrandSyncRes>> SyncBrandToWooCommerceAsync(
            int brandId,
            int siteId,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceBrandSyncRes>
            {
                Data = new WooCommerceBrandSyncRes { BrandId = brandId, Success = false }
            };

            try
            {
                var brand = await _brandStorage.GetBrandAsync(brandId, cancelToken);
                if (brand == null)
                    return CreateResponse(response, StatusCode.ItemNotFound, "Brand not found");

                var (httpClient, baseUrl, ok) = await TryOpenWooClientForSiteAsync(siteId, cancelToken);
                if (!ok || httpClient == null || baseUrl == null)
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce is not enabled or configured for this site");
                }

                using (httpClient)
                {
                    // Resolve parent woo-id if the local brand has a parent that's already synced.
                    int? parentWooId = null;
                    if (brand.ParentBrandId.HasValue)
                    {
                        var parent = await _brandStorage.GetBrandAsync(brand.ParentBrandId.Value, cancelToken);
                        if (parent?.WooCommerceBrandId.HasValue == true)
                            parentWooId = parent.WooCommerceBrandId.Value;
                    }

                    var wooId = await SyncBrandAsync(baseUrl, brand, parentWooId, httpClient, cancelToken);

                    if (wooId.HasValue)
                    {
                        await _brandStorage.UpdateBrandWooCommerceIdAsync(brandId, wooId.Value, cancelToken);
                        response.Data.WooCommerceBrandId = wooId.Value;
                        response.Data.Success = true;
                        response.Data.Message = "Brand synced successfully";
                    }
                    else
                    {
                        response.Data.Success = false;
                        response.Data.Message = "Failed to sync brand to WooCommerce";
                        _logger.LogWarning("WooCommerce sync brand failed: BrandId={BrandId}, site {SiteId}, no Woo id returned", brandId, siteId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WooCommerce sync brand failed: BrandId={BrandId}, site {SiteId}, Error={Error}", brandId, siteId, ex.Message);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }

            return response;
        }

        /// <summary>
        /// Deletes a brand from WooCommerce. Mirrors <see cref="DeleteCategoryFromWooCommerceAsync"/>.
        /// Returns true on 200/204; false on any error (logged).
        /// </summary>
        public async Task<bool> DeleteBrandFromWooCommerceAsync(int siteId, int wooCommerceBrandId, CancellationToken cancelToken)
        {
            var (httpClient, baseUrl, ok) = await TryOpenWooClientForSiteAsync(siteId, cancelToken);
            if (!ok || httpClient == null || baseUrl == null) return false;

            using (httpClient)
            {
                var deleteUrl = $"{baseUrl}/products/brands/{wooCommerceBrandId}?force=true";
                var deleteResponse = await httpClient.DeleteAsync(deleteUrl, cancelToken);
                if (deleteResponse.IsSuccessStatusCode) return true;

                // 404 from a pre-9.6 store ⇒ noop (no Brands taxonomy on that store).
                if (deleteResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return true;

                var errorContent = await deleteResponse.Content.ReadAsStringAsync(cancelToken);
                _logger.LogWarning("Failed to delete brand {WooBrandId} from WooCommerce for site {SiteId}: {Status} {Error}", wooCommerceBrandId, siteId, deleteResponse.StatusCode, errorContent);
                return false;
            }
        }

        /// <summary>
        /// Assigns a flat list of WooCommerce brand IDs to a WooCommerce product
        /// (PUT /products/{wooProductId} with body <c>{"brands":[16,21]}</c>).
        /// Pass an empty array to clear all brands. Returns true on 2xx.
        /// </summary>
        public async Task<bool> AssignProductBrandsAsync(
            int siteId,
            int wooProductId,
            IEnumerable<int> wooBrandIds,
            CancellationToken cancelToken)
        {
            var (httpClient, baseUrl, ok) = await TryOpenWooClientForSiteAsync(siteId, cancelToken);
            if (!ok || httpClient == null || baseUrl == null) return false;

            using (httpClient)
            {
                var body = new WooProductBrandsAssignmentBody
                {
                    Brands = wooBrandIds?.Distinct().ToArray() ?? Array.Empty<int>(),
                };

                var url = $"{baseUrl}/products/{wooProductId}";
                var json = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await httpClient.PutAsync(url, content, cancelToken);
                if (resp.IsSuccessStatusCode) return true;

                var errBody = await resp.Content.ReadAsStringAsync(cancelToken);
                _logger.LogWarning("Failed to assign brands to product {WooProductId} for site {SiteId}: {Status} {Error}",
                    wooProductId, siteId, resp.StatusCode, errBody);
                return false;
            }
        }

        //*************************    Private helpers    *************************//

        /// <summary>
        /// Opens an authenticated HttpClient for a site's WooCommerce REST API. Returns the
        /// client + baseUrl when the site is Woo-linked. Caller must dispose the client.
        /// </summary>
        private async Task<(HttpClient? client, string? baseUrl, bool ok)> TryOpenWooClientForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null
                || site.WooCommerceEnabled != true
                || string.IsNullOrEmpty(site.WooCommerceUrl)
                || string.IsNullOrEmpty(site.WooCommerceKey)
                || string.IsNullOrEmpty(site.WooCommerceSecret))
            {
                return (null, null, false);
            }

            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

            var http = _httpClientFactory.CreateClient();
            http.Timeout = WooCommerceHttpTimeout;
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

            return (http, baseUrl, true);
        }

        /// <summary>
        /// Pages through GET /products/brands?per_page=100 and accumulates results.
        /// Returns an empty list if the endpoint 404s (pre-9.6 store).
        /// </summary>
        private async Task<List<WooBrandResponse>> GetAllBrandsAsync(string baseUrl, HttpClient httpClient, CancellationToken cancelToken)
        {
            var all = new List<WooBrandResponse>();
            int page = 1;
            const int perPage = 100;

            while (true)
            {
                cancelToken.ThrowIfCancellationRequested();

                var url = $"{baseUrl}/products/brands?per_page={perPage}&page={page}&hide_empty=false";
                var resp = await httpClient.GetAsync(url, cancelToken);

                if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("WooCommerce /products/brands returned 404 — store is on a pre-9.6 WooCommerce; skipping brand sync.");
                    return all;
                }

                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(cancelToken);
                    _logger.LogWarning("Failed to list brands from WooCommerce: {Status} {Error}", resp.StatusCode, err);
                    return all;
                }

                var body = await resp.Content.ReadAsStringAsync(cancelToken);
                var page404 = TryDeserializeFromResponse<List<WooBrandResponse>>(body, url, "GET") ?? new List<WooBrandResponse>();
                if (page404.Count == 0) break;

                all.AddRange(page404);

                if (page404.Count < perPage) break; // last page
                page++;
            }

            return all;
        }

        /// <summary>
        /// Pull-direction: fetch brands from a Woo store, upsert into the local Brand table for
        /// the given account+site, and return a wooId → localBrandId map for use during product
        /// import. Brands missing locally are created; brands matched by WooCommerceBrandId or
        /// (case-insensitive) name are updated and have their site link ensured.
        /// Mirrors the Categories upsert pattern in <see cref="WooCommerceService.cs"/>.
        /// </summary>
        public async Task<Dictionary<int, int>> UpsertBrandsFromWooAsync(
            GeorgeDBContext db,
            Site siteForImport,
            int? accountId,
            HttpClient httpClient,
            string baseUrl,
            WooCommerceImportFromWooRes stats,
            CancellationToken cancelToken)
        {
            var map = new Dictionary<int, int>();

            var wooBrands = await GetAllBrandsAsync(baseUrl, httpClient, cancelToken);
            if (wooBrands.Count == 0)
                return map;

            // Pre-load relevant local brands once: scoped to the account, optionally pre-matched
            // by Woo id or by name. Keeps per-row queries low.
            var existingByWooId = await db.Brand
                .Where(b => !b.IsDeleted
                    && b.AccountId == accountId
                    && b.WooCommerceBrandId != null)
                .ToDictionaryAsync(b => b.WooCommerceBrandId!.Value, cancelToken);

            var existingByNameLower = await db.Brand
                .Where(b => !b.IsDeleted && b.AccountId == accountId)
                .ToListAsync(cancelToken);
            var existingByName = existingByNameLower
                .GroupBy(b => (b.Name ?? string.Empty).Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var wb in wooBrands)
            {
                if (string.IsNullOrWhiteSpace(wb.name)) continue;
                var name = wb.name.Trim();
                Brand? local = null;

                if (existingByWooId.TryGetValue(wb.id, out var byId))
                {
                    local = byId;
                }
                else if (existingByName.TryGetValue(name.ToLowerInvariant(), out var byName))
                {
                    local = byName;
                }

                if (local == null)
                {
                    local = new Brand
                    {
                        Name = name,
                        Slug = string.IsNullOrWhiteSpace(wb.slug) ? null : wb.slug,
                        Description = wb.description,
                        AccountId = accountId,
                        WooCommerceBrandId = wb.id,
                        ImageUrl = wb.image?.src,
                        IsEnabled = true,
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                    };
                    db.Brand.Add(local);
                    await db.SaveChangesAsync(cancelToken);
                    stats.Brands.Created++;
                }
                else
                {
                    var changed = false;
                    if (local.WooCommerceBrandId != wb.id)
                    {
                        local.WooCommerceBrandId = wb.id;
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(wb.slug) && local.Slug != wb.slug)
                    {
                        local.Slug = wb.slug;
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(wb.description) && local.Description != wb.description)
                    {
                        local.Description = wb.description;
                        changed = true;
                    }
                    if (!string.IsNullOrWhiteSpace(wb.image?.src) && local.ImageUrl != wb.image!.src)
                    {
                        local.ImageUrl = wb.image.src;
                        changed = true;
                    }
                    if (changed)
                    {
                        local.UpdatedDate = DateTime.UtcNow;
                        await db.SaveChangesAsync(cancelToken);
                        stats.Brands.Updated++;
                    }
                }

                // Ensure the brand is linked to this site so list-by-site queries pick it up.
                var dbBrand = await db.Brand.Include(b => b.Site)
                    .FirstOrDefaultAsync(b => b.Id == local.Id, cancelToken);
                if (dbBrand != null && !dbBrand.Site.Any(s => s.Id == siteForImport.Id))
                {
                    dbBrand.Site.Add(siteForImport);
                    await db.SaveChangesAsync(cancelToken);
                }

                map[wb.id] = local.Id;
            }

            // Resolve parent links once all brands exist.
            foreach (var wb in wooBrands)
            {
                if (!wb.parent.HasValue || wb.parent.Value == 0) continue;
                if (!map.TryGetValue(wb.id, out var localId)) continue;
                if (!map.TryGetValue(wb.parent.Value, out var parentLocalId)) continue;

                var dbBrand = await db.Brand.FirstOrDefaultAsync(b => b.Id == localId, cancelToken);
                if (dbBrand != null && dbBrand.ParentBrandId != parentLocalId)
                {
                    dbBrand.ParentBrandId = parentLocalId;
                    dbBrand.UpdatedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancelToken);
                }
            }

            return map;
        }

        /// <summary>
        /// Replace the ProductBrand join rows on a product with the local brand IDs derived from a
        /// Woo wooBrandId list. The first id in the input is marked as IsPrimary. Deletes existing
        /// rows that aren't in the new list. Skips silently if no brands are mapped (so we don't
        /// stomp existing assignments when the source data was missing).
        /// </summary>
        internal static async Task SetProductBrandsFromWooAsync(
            GeorgeDBContext db,
            Product product,
            IReadOnlyList<int> wooBrandIds,
            IReadOnlyDictionary<int, int> wooToLocalBrandMap,
            CancellationToken cancelToken)
        {
            if (wooBrandIds == null || wooBrandIds.Count == 0) return;

            var localIds = wooBrandIds
                .Select(id => wooToLocalBrandMap.TryGetValue(id, out var lid) ? (int?)lid : null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            if (localIds.Count == 0) return;

            var existing = await db.ProductBrand
                .Where(pb => pb.ProductId == product.Id)
                .ToListAsync(cancelToken);

            // Remove rows for brands no longer present.
            var toRemove = existing.Where(pb => !localIds.Contains(pb.BrandId)).ToList();
            if (toRemove.Count > 0)
                db.ProductBrand.RemoveRange(toRemove);

            // Insert missing rows.
            var existingIds = existing.Select(pb => pb.BrandId).ToHashSet();
            for (int i = 0; i < localIds.Count; i++)
            {
                var bid = localIds[i];
                if (existingIds.Contains(bid)) continue;
                db.ProductBrand.Add(new ProductBrand
                {
                    ProductId = product.Id,
                    BrandId = bid,
                    IsPrimary = i == 0,
                });
            }

            // Update IsPrimary on the first id (rest become non-primary).
            foreach (var pb in existing)
                pb.IsPrimary = pb.BrandId == localIds[0];

            // Back-compat: keep the legacy single Product.BrandId pointing at the primary.
            product.BrandId = localIds[0];

            await db.SaveChangesAsync(cancelToken);
        }

        /// <summary>
        /// Lower-level: create-or-update a brand in Woo. If the local Brand has a WooCommerceBrandId
        /// we PUT first; if that fails (term gone), we POST. On term_exists conflict, reuses the
        /// existing id and PUTs description/parent updates onto it.
        /// </summary>
        private async Task<int?> SyncBrandAsync(
            string baseUrl,
            Brand brand,
            int? parentWooId,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var payload = new WooBrandPayload
            {
                name = brand.Name,
                slug = string.IsNullOrWhiteSpace(brand.Slug) ? null : brand.Slug,
                description = brand.Description ?? string.Empty,
                parent = parentWooId, // null means "no parent" — Woo accepts this
                image = string.IsNullOrWhiteSpace(brand.ImageUrl) ? null : new WooBrandImage { src = brand.ImageUrl },
            };

            // 1) Try update if we already have a Woo id.
            if (brand.WooCommerceBrandId.HasValue)
            {
                var updatedId = await TryUpdateBrandAsync(baseUrl, brand.WooCommerceBrandId.Value, payload, httpClient, cancelToken);
                if (updatedId.HasValue) return updatedId.Value;
            }

            // 2) Otherwise POST.
            var createUrl = $"{baseUrl}/products/brands";
            var createJson = JsonSerializer.Serialize(payload, JsonOptionsIgnoreNulls);
            using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

            var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
            var responseBody = await createResponse.Content.ReadAsStringAsync(cancelToken);

            if (createResponse.IsSuccessStatusCode)
            {
                var created = TryDeserializeFromResponse<WooBrandResponse>(responseBody, createUrl, "POST");
                return created?.id;
            }

            // 404 ⇒ pre-9.6 Woo with no Brands taxonomy. Skip silently; caller continues.
            if (createResponse.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("WooCommerce /products/brands missing on this store; skipping brand push.");
                return null;
            }

            // Handle term_exists: a brand with the same slug already exists. Update it.
            var wooErr = TryDeserialize<WooErrorResponse>(responseBody);
            if (wooErr?.code == "term_exists" && wooErr.data?.resource_id is int existingId)
            {
                var updatedId = await TryUpdateBrandAsync(baseUrl, existingId, payload, httpClient, cancelToken);
                return updatedId ?? existingId;
            }

            throw new Exception(GetUserFriendlyWooCommerceError((int)createResponse.StatusCode, responseBody));
        }

        private async Task<int?> TryUpdateBrandAsync(
            string baseUrl,
            int wooBrandId,
            WooBrandPayload payload,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/brands/{wooBrandId}";
            var updateJson = JsonSerializer.Serialize(payload, JsonOptionsIgnoreNulls);
            using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

            var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
            var responseBody = await updateResponse.Content.ReadAsStringAsync(cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return null;

            var updated = TryDeserializeFromResponse<WooBrandResponse>(responseBody, updateUrl, "PUT");
            return updated?.id;
        }

        // Don't send "image": null when the brand has no logo — Woo treats null as "remove the existing one".
        private static readonly JsonSerializerOptions JsonOptionsIgnoreNulls = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>Result of a single brand → WooCommerce sync.</summary>
    public class WooCommerceBrandSyncRes
    {
        public int BrandId { get; set; }
        public int? WooCommerceBrandId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
