using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace George.Services
{
    /// <summary>
    /// Brand business logic. Mirrors <see cref="CategoryService"/>, including the on-save
    /// WooCommerce push for any sites the brand is linked to.
    /// </summary>
    public class BrandService : ServiceBase
    {
        private readonly BrandStorage _brandStorage;
        private readonly WooCommerceService _wooCommerceService;

        public BrandService(
            ILogger<BrandService> logger,
            IMapper mapper,
            CacheManager cache,
            BrandStorage brandStorage,
            WooCommerceService wooCommerceService)
            : base(logger, mapper, cache)
        {
            _brandStorage = brandStorage;
            _wooCommerceService = wooCommerceService;
        }

        public async Task<IApiResponse<ApiListResponse<BrandRes>>> GetBrandsAsync(
            ApiListReq<BrandFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<BrandRes>>
            {
                Data = new ApiListResponse<BrandRes>()
            };

            var res = await _brandStorage.GetBrandsAsync(request.Filter, request, cancelToken);

            var items = new List<BrandRes>(res.Items.Count);
            foreach (var b in res.Items)
            {
                var dto = MapBrandToRes(b);

                if (request.Filter?.IncludeProductCount == true)
                {
                    dto.ProductCount = await _brandStorage.CountProductsAsync(b.Id, cancelToken);
                }

                items.Add(dto);
            }

            response.Data!.Items = items;
            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<BrandRes>> GetBrandAsync(int brandId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<BrandRes>();

            var brand = await _brandStorage.GetBrandAsync(brandId, cancelToken);
            if (brand == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapBrandToRes(brand);
            response.Data.ProductCount = await _brandStorage.CountProductsAsync(brand.Id, cancelToken);

            return response;
        }

        public async Task<IApiResponse<BrandRes>> CreateBrandAsync(CreateBrandReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<BrandRes>();

            // De-duplicate by name within (account, parent). Per spec §7, the API "doesn't block"
            // duplicate names — we do that here. If a same-named brand already exists in scope,
            // we treat the request as a no-op create and return the existing brand instead of
            // inserting a duplicate.
            var trimmedName = (req.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedName))
                return CreateResponse(response, StatusCode.InvalidRequest, "Brand name is required.");

            var existing = await _brandStorage.FindBrandByNameAsync(
                trimmedName,
                req.ParentBrandId,
                req.AccountId,
                req.SiteIds,
                cancelToken);

            if (existing != null)
            {
                // If the existing brand is missing any of the requested sites, link them now —
                // mirrors CategoryStorage.EnsureCategoryHasSitesAsync behaviour.
                if (req.SiteIds != null && req.SiteIds.Any())
                {
                    await _brandStorage.EnsureBrandHasSitesAsync(existing.Id, req.SiteIds, cancelToken);
                }
                var refreshed = await _brandStorage.GetBrandAsync(existing.Id, cancelToken);
                response.Data = MapBrandToRes(refreshed!);
                return response;
            }

            // Map req → entity and stamp audit fields
            var model = _mapper.Map<Brand>(req);
            model.Name = trimmedName;
            model.Slug = NormalizeSlug(req.Slug, fallbackName: trimmedName);
            model.IsDeleted = false;
            model.CreationTime = DateTime.UtcNow;
            model.CreationUserId = AuthUser.Id;
            model.IsEnabled ??= true;

            var created = await _brandStorage.CreateBrandAsync(model, req.SiteIds, cancelToken).ConfigureAwait(false);
            if (created != null)
            {
                var refreshed = await _brandStorage.GetBrandAsync(created.Id, cancelToken);
                response.Data = MapBrandToRes(refreshed!);

                // Push to WooCommerce on every Woo-linked site this brand is attached to.
                if (refreshed?.Site != null && refreshed.Site.Any())
                    await SyncBrandToWooCommerceForEnabledSitesAsync(refreshed.Id, refreshed.Site, cancelToken);
            }

            return response;
        }

        public async Task<IApiResponse<BrandRes>> UpdateBrandAsync(int brandId, UpdateBrandReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<BrandRes>();

            var existingBrand = await _brandStorage.GetBrandAsync(brandId, cancelToken);
            if (existingBrand == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Map req → entity. Re-stamp Id (route wins over body) and audit user.
            var model = _mapper.Map<Brand>(req);
            model.Id = brandId;
            model.Name = (req.Name ?? string.Empty).Trim();
            model.Slug = NormalizeSlug(req.Slug, fallbackName: model.Name);
            // Client updates omit Woo/source IDs — never wipe sync metadata already stored.
            model.WooCommerceBrandId = req.WooCommerceBrandId ?? existingBrand.WooCommerceBrandId;
            model.SourceGlobalBrandId = req.SourceGlobalBrandId ?? existingBrand.SourceGlobalBrandId;
            model.UpdateUserId = AuthUser.Id;

            var updated = await _brandStorage.UpdateBrandAsync(model, req.SiteIds, cancelToken).ConfigureAwait(false);
            if (updated != null)
            {
                var refreshed = await _brandStorage.GetBrandAsync(updated.Id, cancelToken);
                response.Data = MapBrandToRes(refreshed!);

                if (refreshed?.Site != null && refreshed.Site.Any())
                    await SyncBrandToWooCommerceForEnabledSitesAsync(refreshed.Id, refreshed.Site, cancelToken);
            }

            return response;
        }

        /// <param name="siteId">When provided, only unlinks the brand from this site (BrandSite row)
        /// and leaves the brand on other sites. When null, soft-deletes the brand entirely.</param>
        public async Task<IApiResponse<bool>> DeleteBrandAsync(int brandId, int? siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            if (siteId.HasValue)
            {
                // Per-site unlink. If the brand is synced to Woo on this site, also remove there.
                var brand = await _brandStorage.GetBrandAsync(brandId, cancelToken);
                if (brand?.WooCommerceBrandId.HasValue == true && brand.Site != null
                    && brand.Site.Any(s => s.Id == siteId.Value && s.WooCommerceEnabled == true))
                {
                    try
                    {
                        var deleted = await _wooCommerceService.DeleteBrandFromWooCommerceAsync(siteId.Value, brand.WooCommerceBrandId.Value, cancelToken);
                        if (deleted)
                            _logger.LogInformation("Removed brand {BrandId} (Woo id {WooId}) from WooCommerce for site {SiteId}", brandId, brand.WooCommerceBrandId, siteId.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error removing brand {BrandId} from WooCommerce for site {SiteId}", brandId, siteId.Value);
                    }
                }

                response.Data = await _brandStorage.RemoveBrandFromSiteAsync(brandId, siteId.Value, cancelToken);
                return response;
            }

            // Full delete: soft-delete and remove from Woo on every linked site.
            var brandForDelete = await _brandStorage.GetBrandAsync(brandId, cancelToken);
            if (brandForDelete?.WooCommerceBrandId.HasValue == true && brandForDelete.Site != null && brandForDelete.Site.Any())
            {
                foreach (var site in brandForDelete.Site.Where(s => s.WooCommerceEnabled == true))
                {
                    try
                    {
                        var deleted = await _wooCommerceService.DeleteBrandFromWooCommerceAsync(site.Id, brandForDelete.WooCommerceBrandId.Value, cancelToken);
                        if (deleted)
                            _logger.LogInformation("Deleted brand {BrandId} (Woo id {WooId}) from WooCommerce for site {SiteId}", brandId, brandForDelete.WooCommerceBrandId, site.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting brand {BrandId} from WooCommerce for site {SiteId}", brandId, site.Id);
                    }
                }
            }

            response.Data = await _brandStorage.DeleteBrandAsync(brandId, cancelToken);
            return response;
        }

        /// <summary>
        /// Pushes a brand to every Woo-linked site, swallowing per-site errors so a single
        /// failed sync doesn't block the brand-create/update operation.
        /// </summary>
        private async Task SyncBrandToWooCommerceForEnabledSitesAsync(
            int brandId,
            ICollection<Site> sites,
            CancellationToken cancelToken)
        {
            var enabledSites = sites.Where(s => s.WooCommerceEnabled == true).ToList();
            if (!enabledSites.Any()) return;

            foreach (var site in enabledSites)
            {
                try
                {
                    var syncResponse = await _wooCommerceService.SyncBrandToWooCommerceAsync(brandId, site.Id, cancelToken);
                    if (syncResponse.Data?.Success != true)
                    {
                        _logger.LogWarning(
                            "Failed to sync brand {BrandId} to WooCommerce for site {SiteId}: {Message}",
                            brandId, site.Id, syncResponse.Data?.Message ?? "Unknown error");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Synced brand {BrandId} to WooCommerce for site {SiteId} (Woo id {WooId})",
                            brandId, site.Id, syncResponse.Data?.WooCommerceBrandId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error syncing brand {BrandId} to WooCommerce for site {SiteId}",
                        brandId, site.Id);
                }
            }
        }

        //*************************    Helpers    *************************//

        private static BrandRes MapBrandToRes(Brand brand)
        {
            var res = new BrandRes
            {
                Id = brand.Id,
                CreationTime = brand.CreationTime,
                UpdatedDate = brand.UpdatedDate,
                CreationUserId = brand.CreationUserId,
                Name = brand.Name,
                Slug = brand.Slug,
                Description = brand.Description,
                ParentBrandId = brand.ParentBrandId,
                SortOrder = brand.SortOrder,
                IsEnabled = brand.IsEnabled,
                AccountId = brand.AccountId,
                ImageUrl = brand.ImageUrl,
                IconUrl = brand.IconUrl,
                SeoTitle = brand.SeoTitle,
                SeoDescription = brand.SeoDescription,
                WooCommerceBrandId = brand.WooCommerceBrandId,
                SourceGlobalBrandId = brand.SourceGlobalBrandId,
            };

            if (brand.Site != null && brand.Site.Any())
                res.SiteIds = brand.Site.Select(s => s.Id).ToList();

            return res;
        }

        /// <summary>
        /// If the caller provided a slug, normalize whitespace & lowercase but preserve characters
        /// (incl. Hebrew). If they didn't, derive a best-effort slug from the brand name —
        /// lowercase, replace whitespace with '-', strip punctuation. Returns null if the result
        /// would be empty (e.g. all-symbol input) — let the DB hold null and let WooCommerce
        /// auto-generate one when we sync.
        /// </summary>
        internal static string? NormalizeSlug(string? providedSlug, string fallbackName)
        {
            string source = !string.IsNullOrWhiteSpace(providedSlug) ? providedSlug! : fallbackName ?? string.Empty;
            source = source.Trim().ToLowerInvariant();
            if (source.Length == 0) return null;

            var sb = new StringBuilder(source.Length);
            bool prevDash = false;
            foreach (var ch in source)
            {
                var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
                bool keep =
                    cat == UnicodeCategory.LowercaseLetter ||
                    cat == UnicodeCategory.UppercaseLetter ||
                    cat == UnicodeCategory.OtherLetter || // Hebrew/Arabic/CJK fall in here
                    cat == UnicodeCategory.DecimalDigitNumber;

                if (keep)
                {
                    sb.Append(ch);
                    prevDash = false;
                }
                else if (!prevDash && sb.Length > 0)
                {
                    sb.Append('-');
                    prevDash = true;
                }
            }

            // Trim trailing dash.
            while (sb.Length > 0 && sb[sb.Length - 1] == '-')
                sb.Length--;

            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
