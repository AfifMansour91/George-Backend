using AutoMapper;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Attribute = George.DB.Attribute;
using System.Globalization;

namespace George.Services
{
    public class WooCommerceService : ServiceBase
    {
        /// <summary>HTTP client timeout for WooCommerce API calls (bulk sync can take many minutes).</summary>
        private static readonly TimeSpan WooCommerceHttpTimeout = TimeSpan.FromMinutes(30);

        /// <summary>Semaphore per (siteId, attributeName) so parallel product syncs don't create the same global attribute multiple times.</summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> AttributeEnsureLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        private readonly SiteStorage _siteStorage;
        private readonly CategoryStorage _categoryStorage;
        private readonly ProductStorage _productStorage;
        private readonly AttributeStorage _attributeStorage;
        private readonly OrderStorage _orderStorage;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServiceScopeFactory _scopeFactory;

        public WooCommerceService(
            ILogger<WooCommerceService> logger,
            IMapper mapper,
            CacheManager cache,
            SiteStorage siteStorage,
            CategoryStorage categoryStorage,
            ProductStorage productStorage,
            AttributeStorage attributeStorage,
            OrderStorage orderStorage,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory
        ) : base(logger, mapper, cache)
        {
            _siteStorage = siteStorage;
            _categoryStorage = categoryStorage;
            _productStorage = productStorage;
            _attributeStorage = attributeStorage;
            _orderStorage = orderStorage;
            _httpClientFactory = httpClientFactory;
            _scopeFactory = scopeFactory;
        }

        public async Task<IApiResponse<WooCommerceSyncRes>> SyncToWooCommerceAsync(
            WooCommerceSyncReq req,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceSyncRes>
            {
                Data = new WooCommerceSyncRes()
            };

            try
            {
                cancelToken.ThrowIfCancellationRequested();
                // Get site with WooCommerce credentials
                var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken);
                if (site == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                }

                if (string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce integration not configured. Please set up your credentials in Store Settings.");
                }

                // Setup WooCommerce API client (long timeout for bulk sync: many products can take several minutes)
                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
                
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = WooCommerceHttpTimeout;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");
                // Note: Content-Type is set on HttpContent objects (StringContent), not on DefaultRequestHeaders

                _logger.LogInformation("WooCommerce sync started for site {SiteId} (product filter: {Filter})", req.SiteId, req.ProductIds != null ? string.Join(",", req.ProductIds) : "all");

                // Sync categories first
                var categoryMap = await SyncCategoriesAsync(baseUrl, req.SiteId, httpClient, cancelToken);
                _logger.LogInformation("WooCommerce sync: categories completed for site {SiteId}, {Count} categories mapped", req.SiteId, categoryMap.Count);

                // Sync products
                var syncResults = await SyncProductsAsync(
                    baseUrl,
                    req.SiteId,
                    req.ProductIds,
                    categoryMap,
                    httpClient,
                    cancelToken,
                    progress: null);

                response.Data.Success = syncResults.Where(r => r.Success).ToList();
                response.Data.Failed = syncResults.Where(r => !r.Success).ToList();
                var totalAttempted = syncResults.Count;
                var failedCount = response.Data.Failed.Count;
                response.Data.Message = totalAttempted == 0
                    ? "No products to sync."
                    : $"Attempted {totalAttempted} products: {response.Data.Success.Count} succeeded, {failedCount} failed.";

                if (failedCount > 0)
                {
                    _logger.LogWarning("WooCommerce sync completed for site {SiteId} with {Failed} failure(s). Succeeded: {Success}, Failed: {Failed}", req.SiteId, failedCount, response.Data.Success.Count, failedCount);
                    foreach (var f in response.Data.Failed)
                        _logger.LogWarning("WooCommerce sync failed: ProductId={ProductId}, Name={ProductName}, Error={Error}", f.ProductId, f.ProductName ?? "", f.Error ?? "");
                }
                else
                    _logger.LogInformation("WooCommerce sync completed for site {SiteId}: {Count} products synced successfully", req.SiteId, response.Data.Success.Count);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing to WooCommerce");
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        /// <summary>Syncs categories to WooCommerce and returns the list of product IDs to sync. Client can then sync products one-by-one to avoid long-lived streams (QUIC/proxy errors).</summary>
        public async Task<List<int>> SyncCategoriesAndGetProductIdsAsync(WooCommerceSyncReq req, CancellationToken cancelToken)
        {
            var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken);
            if (site == null)
                throw new InvalidOperationException("Site not found");
            if (string.IsNullOrEmpty(site.WooCommerceUrl) || string.IsNullOrEmpty(site.WooCommerceKey) || string.IsNullOrEmpty(site.WooCommerceSecret))
                throw new InvalidOperationException("WooCommerce integration not configured. Please set up your credentials in Store Settings.");

            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = WooCommerceHttpTimeout;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

            await SyncCategoriesAsync(baseUrl, req.SiteId, httpClient, cancelToken);

            if (req.ProductIds != null && req.ProductIds.Any())
                return req.ProductIds.Distinct().ToList();
            return await _productStorage.GetProductIdsForSiteAsync(req.SiteId, cancelToken);
        }

        /// <summary>Syncs to WooCommerce and reports progress. Used by streaming endpoint. Throws on validation failure.</summary>
        public async Task<WooCommerceSyncRes> SyncToWooCommerceWithProgressAsync(
            WooCommerceSyncReq req,
            IProgress<WooCommerceSyncProgress> progress,
            CancellationToken cancelToken)
        {
            var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken);
            if (site == null)
                throw new InvalidOperationException("Site not found");
            if (string.IsNullOrEmpty(site.WooCommerceUrl) || string.IsNullOrEmpty(site.WooCommerceKey) || string.IsNullOrEmpty(site.WooCommerceSecret))
                throw new InvalidOperationException("WooCommerce integration not configured. Please set up your credentials in Store Settings.");

            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = WooCommerceHttpTimeout;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

            _logger.LogInformation("WooCommerce sync (with progress) started for site {SiteId}", req.SiteId);

            var categoryMap = await SyncCategoriesAsync(baseUrl, req.SiteId, httpClient, cancelToken);
            _logger.LogInformation("WooCommerce sync: categories completed for site {SiteId}, {Count} categories mapped", req.SiteId, categoryMap.Count);

            var syncResults = await SyncProductsAsync(
                baseUrl,
                req.SiteId,
                req.ProductIds,
                categoryMap,
                httpClient,
                cancelToken,
                progress);

            var successList = syncResults.Where(r => r.Success).ToList();
            var failedList = syncResults.Where(r => !r.Success).ToList();
            var totalAttempted = syncResults.Count;
            var message = totalAttempted == 0
                ? "No products to sync."
                : $"Attempted {totalAttempted} products: {successList.Count} succeeded, {failedList.Count} failed.";

            if (failedList.Count > 0)
            {
                _logger.LogWarning("WooCommerce sync (with progress) completed for site {SiteId} with {Failed} failure(s). Succeeded: {Success}, Failed: {Failed}", req.SiteId, failedList.Count, successList.Count, failedList.Count);
                foreach (var f in failedList)
                    _logger.LogWarning("WooCommerce sync failed: ProductId={ProductId}, Name={ProductName}, Error={Error}", f.ProductId, f.ProductName ?? "", f.Error ?? "");
            }
            else
                _logger.LogInformation("WooCommerce sync (with progress) completed for site {SiteId}: {Count} products synced successfully", req.SiteId, successList.Count);

            return new WooCommerceSyncRes
            {
                Message = message,
                Success = successList,
                Failed = failedList
            };
        }

        /// <summary>
        /// Verifies sync consistency for an account (and optionally a single site): product counts per site,
        /// duplicate SKUs within a site, and cross-site SKU overlap (same raw SKU in different sites).
        /// Use this to confirm data syncs without collision between branches.
        /// </summary>
        public async Task<IApiResponse<WooCommerceSyncVerificationRes>> VerifySyncAsync(int accountId, int? siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceSyncVerificationRes> { Data = new WooCommerceSyncVerificationRes() };
            var result = response.Data;
            List<Site> sites;
            if (siteId.HasValue)
            {
                var site = await _siteStorage.GetSiteAsync(siteId.Value, cancelToken);
                sites = site != null && site.AccountId == accountId ? new List<Site> { site } : new List<Site>();
            }
            else
            {
                sites = await _siteStorage.GetSitesByAccountAsync(accountId, cancelToken);
            }

            if (sites.Count == 0)
            {
                result.Message = siteId.HasValue ? "Site not found or does not belong to account." : "No sites found for account.";
                return response;
            }

            // Load all products for the account (with Site) to compute per-site stats and cross-site SKU overlap
            var productsResult = await _productStorage.GetProductsAsync(
                new ProductFilter { AccountId = accountId },
                new PagingExDto { Skip = 0, Take = 50000, IncludeTotal = false },
                cancelToken);
            var allProducts = productsResult.Items;

            var siteIdsSet = sites.Select(s => s.Id).ToHashSet();
            var crossSiteSkuToSites = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var site in sites)
            {
                var siteProducts = allProducts.Where(p => p.Site != null && p.Site.Any(s => s.Id == site.Id)).ToList();
                var withSku = siteProducts.Count(p => !string.IsNullOrWhiteSpace(p.Sku));
                var withWooId = siteProducts.Count(p => p.WooCommerceId.HasValue);
                var skuCounts = siteProducts
                    .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
                    .GroupBy(p => (p.Sku ?? "").Trim(), StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();

                foreach (var p in siteProducts.Where(p => !string.IsNullOrWhiteSpace(p.Sku)))
                {
                    var skuNorm = (p.Sku ?? "").Trim();
                    if (!crossSiteSkuToSites.TryGetValue(skuNorm, out var set))
                    {
                        set = new HashSet<int>();
                        crossSiteSkuToSites[skuNorm] = set;
                    }
                    foreach (var s in p.Site ?? Array.Empty<Site>())
                        if (siteIdsSet.Contains(s.Id))
                            set.Add(s.Id);
                }

                result.Sites.Add(new SiteSyncVerificationReport
                {
                    SiteId = site.Id,
                    SiteName = site.SiteName ?? "",
                    ProductCount = siteProducts.Count,
                    WithSkuCount = withSku,
                    WithWooCommerceIdCount = withWooId,
                    DuplicateSkusInSite = skuCounts,
                    WooCommerceConfigured = !string.IsNullOrEmpty(site.WooCommerceUrl) &&
                                          !string.IsNullOrEmpty(site.WooCommerceKey) &&
                                          !string.IsNullOrEmpty(site.WooCommerceSecret)
                });
            }

            result.CrossSiteSkuOverlap = crossSiteSkuToSites
                .Where(kv => kv.Value.Count > 1)
                .Select(kv => kv.Key)
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();

            result.AllSitesOk = result.Sites.All(s => s.DuplicateSkusInSite.Count == 0) && result.CrossSiteSkuOverlap.Count == 0;
            result.Message = result.AllSitesOk
                ? "All sites OK: no duplicate SKUs within a site and no cross-site SKU overlap (after using site-prefixed SKU in WooCommerce, overlap does not cause collision)."
                : (result.CrossSiteSkuOverlap.Count > 0
                    ? $"Found {result.CrossSiteSkuOverlap.Count} SKU(s) used in more than one site. With site-prefixed SKU (S{{siteId}}_) in WooCommerce these do not collide."
                    : "Found duplicate SKUs within at least one site. Fix duplicates so sync is consistent.");
            return response;
        }

        private async Task<Dictionary<int, int>> SyncCategoriesAsync(
            string baseUrl,
            int siteId,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var categoryMap = new Dictionary<int, int>();

            // Get all categories linked to this site through the CategorySite junction table
            var categoriesResult = await _categoryStorage.GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId },
                new PagingExDto { Skip = 0, Take = 10000, IncludeTotal = false },
                cancelToken);

            cancelToken.ThrowIfCancellationRequested();
            // Filter out deleted and inactive categories
            var categories = categoriesResult.Items
                .Where(c => !c.IsDeleted && c.IsActive)
                .ToList();

            _logger.LogInformation("Found {Count} categories to sync for site {SiteId} (from CategorySite table)", 
                categories.Count, siteId);

            // Sync main categories first
            var mainCategories = categories
                .Where(c => c.ParentCategoryId == null)
                .ToList();

            foreach (var category in mainCategories)
            {
                cancelToken.ThrowIfCancellationRequested();
                try
                {
                    var wooCatId = await SyncCategoryAsync(baseUrl, category, null, httpClient, cancelToken);
                    if (wooCatId.HasValue)
                    {
                        categoryMap[category.Id] = wooCatId.Value;
                        await _categoryStorage.UpdateCategoryWooCommerceIdAsync(category.Id, wooCatId.Value, cancelToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync category {CategoryName}", category.Name);
                }
            }

            // Sync subcategories
            var subCategories = categories
                .Where(c => c.ParentCategoryId != null && categoryMap.ContainsKey(c.ParentCategoryId.Value))
                .ToList();

            foreach (var category in subCategories)
            {
                cancelToken.ThrowIfCancellationRequested();
                try
                {
                    var parentWooId = categoryMap[category.ParentCategoryId!.Value];
                    var wooCatId = await SyncCategoryAsync(baseUrl, category, parentWooId, httpClient, cancelToken);
                    if (wooCatId.HasValue)
                    {
                        categoryMap[category.Id] = wooCatId.Value;
                        await _categoryStorage.UpdateCategoryWooCommerceIdAsync(category.Id, wooCatId.Value, cancelToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync subcategory {CategoryName}", category.Name);
                }
            }

            if (categoryMap.Count < categories.Count)
                _logger.LogWarning("WooCommerce sync categories: site {SiteId}, {Mapped} of {Total} categories mapped (some failed)", siteId, categoryMap.Count, categories.Count);
            return categoryMap;
        }

        /// <summary>
        /// Syncs a single category to WooCommerce for a specific site
        /// </summary>
        public async Task<IApiResponse<WooCommerceCategorySyncRes>> SyncCategoryToWooCommerceAsync(
            int categoryId,
            int siteId,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceCategorySyncRes>
            {
                Data = new WooCommerceCategorySyncRes()
            };

            try
            {
                // Get category
                var category = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
                if (category == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Category not found");
                }

                // Get site
                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
                if (site == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                }

                // Check if WooCommerce is enabled for this site
                if (site.WooCommerceEnabled != true ||
                    string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce is not enabled or configured for this site");
                }

                // Setup WooCommerce API client (long timeout for sync operations)
                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = WooCommerceHttpTimeout;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                // Get parent WooCommerce ID if category has a parent
                int? parentWooId = null;
                if (category.ParentCategoryId.HasValue)
                {
                    var parentCategory = await _categoryStorage.GetCategoryAsync(category.ParentCategoryId.Value, cancelToken);
                    if (parentCategory?.WooCommerceId.HasValue == true)
                    {
                        parentWooId = parentCategory.WooCommerceId.Value;
                    }
                }

                // Sync the category
                var wooCatId = await SyncCategoryAsync(baseUrl, category, parentWooId, httpClient, cancelToken);

                if (wooCatId.HasValue)
                {
                    // Update category with WooCommerce ID
                    await _categoryStorage.UpdateCategoryWooCommerceIdAsync(categoryId, wooCatId.Value, cancelToken);

                    response.Data.CategoryId = categoryId;
                    response.Data.WooCommerceId = wooCatId.Value;
                    response.Data.Success = true;
                    response.Data.Message = "Category synced successfully";
                }
                else
                {
                    response.Data.Success = false;
                    response.Data.Message = "Failed to sync category to WooCommerce";
                    _logger.LogWarning("WooCommerce sync category failed: CategoryId={CategoryId}, site {SiteId}, no WooCommerce ID returned", categoryId, siteId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WooCommerce sync category failed: CategoryId={CategoryId}, site {SiteId}, Error={Error}", categoryId, siteId, ex.Message);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        /// <summary>
        /// Syncs a single site attribute to WooCommerce product attributes and terms for that site.
        /// </summary>
        public async Task<IApiResponse<WooCommerceAttributeSyncRes>> SyncAttributeToWooCommerceAsync(
            int attributeId,
            int siteId,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceAttributeSyncRes>
            {
                Data = new WooCommerceAttributeSyncRes()
            };

            try
            {
                var attribute = await _attributeStorage.GetAttributeAsync(attributeId, cancelToken);
                if (attribute == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Attribute not found");
                }

                if (attribute.SiteId != siteId)
                {
                    return CreateResponse(response, StatusCode.InvalidRequest, "Attribute does not belong to this site");
                }

                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
                if (site == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                }

                if (site.WooCommerceEnabled != true ||
                    string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce is not enabled or configured for this site");
                }

                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = WooCommerceHttpTimeout;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                var (wooAttrId, _) = await SyncAttributeAsync(baseUrl, attribute, httpClient, cancelToken);

                if (wooAttrId.HasValue)
                {
                    await _attributeStorage.UpdateAttributeWooCommerceIdAsync(attributeId, wooAttrId.Value, cancelToken);
                    response.Data.AttributeId = attributeId;
                    response.Data.WooCommerceId = wooAttrId.Value;
                    response.Data.Success = true;
                    response.Data.Message = "Attribute synced successfully";
                }
                else
                {
                    response.Data.Success = false;
                    response.Data.Message = "Failed to sync attribute to WooCommerce";
                    _logger.LogWarning("WooCommerce sync attribute failed: AttributeId={AttributeId}, site {SiteId}, no WooCommerce ID returned", attributeId, siteId);
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WooCommerce sync attribute failed: AttributeId={AttributeId}, site {SiteId}, Error={Error}", attributeId, siteId, ex.Message);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        private static string SlugifyAttributeName(string name)
        {
            name ??= "";

            // Keep ASCII letters/digits for "normal" names
            var ascii = Regex.Replace(name, @"[^a-zA-Z0-9]+", "_")
                .ToLowerInvariant()
                .Trim('_');

            if (!string.IsNullOrEmpty(ascii))
                return TruncateWooSlug(ascii);

            // Hebrew/Arabic/etc: generate stable slug from hash to avoid collisions
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(name.Trim()));
            var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            return TruncateWooSlug("a_" + hash.Substring(0, 24)); // total <= 28
        }

        private static string TruncateWooSlug(string slug)
        {
            // Woo attribute slug limit is ~28 chars
            return slug.Length <= 28 ? slug : slug.Substring(0, 28);
        }


        /// <summary>
        /// Finds an existing WooCommerce global attribute by name or slug.
        /// Returns (id, slug) if found; (null, null) otherwise. Slug is the actual WooCommerce taxonomy slug (e.g. pa_xxx).
        /// </summary>
        private async Task<(int? id, string? slug)> FindExistingAttributeAsync(string baseUrl, string attributeName, HttpClient httpClient, CancellationToken cancelToken)
        {
            try
            {
                var slug = SlugifyAttributeName(attributeName);
                var searchUrl = $"{baseUrl}/products/attributes?slug={Uri.EscapeDataString(slug)}";

                var searchResponse = await httpClient.GetAsync(searchUrl, cancelToken);
                
                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchBody = await searchResponse.Content.ReadAsStringAsync(cancelToken);
                    var attributes = TryDeserialize<List<WooCommerceAttributeResponse>>(searchBody);
                    if (attributes != null && attributes.Count > 0)
                    {
                        var matchingAttr = attributes.FirstOrDefault(a => 
                            string.Equals(a.name, attributeName, StringComparison.OrdinalIgnoreCase));
                        if (matchingAttr != null)
                        {
                            return (matchingAttr.id, matchingAttr.slug ?? slug);
                        }
                    }
                }

                // Also try searching by name (Hebrew names don't slug well; WooCommerce may have different slug)
                var nameSearchUrl = $"{baseUrl}/products/attributes?per_page=100&page=1";

                var nameSearchResponse = await httpClient.GetAsync(nameSearchUrl, cancelToken);
                
                if (nameSearchResponse.IsSuccessStatusCode)
                {
                    var nameSearchBody = await nameSearchResponse.Content.ReadAsStringAsync(cancelToken);
                    var allAttributes = TryDeserialize<List<WooCommerceAttributeResponse>>(nameSearchBody);
                    if (allAttributes != null)
                    {
                        var matchingByName = allAttributes.FirstOrDefault(a => 
                            string.Equals(a.name, attributeName, StringComparison.OrdinalIgnoreCase));
                        if (matchingByName != null)
                        {
                            return (matchingByName.id, matchingByName.slug ?? slug);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching for existing WooCommerce attribute with name {AttributeName}", attributeName);
            }

            return (null, null);
        }

        /// <summary>
        /// Syncs an attribute to WooCommerce. Returns (id, slug) - slug is the actual WooCommerce taxonomy slug.
        /// </summary>
        private async Task<(int? id, string? slug)> SyncAttributeAsync(string baseUrl, Attribute attribute, HttpClient httpClient, CancellationToken cancelToken)
        {
            var slug = SlugifyAttributeName(attribute.Name);
            var wooAttrData = new { name = attribute.Name, slug, type = "select", order_by = "menu_order", has_archives = false };

            // First, check if we have a stored WooCommerceId and try to update
            if (attribute.WooCommerceId.HasValue)
            {
                var (updatedId, updatedSlug) = await TryUpdateProductAttributeAsync(baseUrl, attribute.WooCommerceId.Value, wooAttrData, httpClient, cancelToken);
                if (updatedId.HasValue)
                {
                    await SyncAttributeTermsAsync(baseUrl, updatedId.Value, attribute, httpClient, cancelToken);
                    return (updatedId.Value, updatedSlug ?? slug);
                }
            }

            // If update failed or no WooCommerceId, try to find existing attribute by name/slug
            var (existingId, existingSlug) = await FindExistingAttributeAsync(baseUrl, attribute.Name, httpClient, cancelToken);
            if (existingId.HasValue)
            {
                await SyncAttributeTermsAsync(baseUrl, existingId.Value, attribute, httpClient, cancelToken);
                return (existingId.Value, existingSlug ?? slug);
            }

            // No existing attribute found, create a new one
            var createUrl = $"{baseUrl}/products/attributes";
            var createJson = JsonSerializer.Serialize(wooAttrData);
            using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
            var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
            var responseBody = await createResponse.Content.ReadAsStringAsync(cancelToken);

            if (createResponse.IsSuccessStatusCode)
            {
                var created = TryDeserializeFromResponse<WooCommerceAttributeResponse>(responseBody, createUrl, "POST");
                if (created?.id is int id)
                {
                    await SyncAttributeTermsAsync(baseUrl, id, attribute, httpClient, cancelToken);
                    return (id, created.slug ?? slug);
                }
            }

            // Create failed (e.g. duplicate from parallel sync); try to find existing again and use it
            var (retryId, retrySlug) = await FindExistingAttributeAsync(baseUrl, attribute.Name, httpClient, cancelToken);
            if (retryId.HasValue)
            {
                await SyncAttributeTermsAsync(baseUrl, retryId.Value, attribute, httpClient, cancelToken);
                return (retryId.Value, retrySlug ?? slug);
            }

            throw new Exception(GetUserFriendlyWooCommerceError((int)createResponse.StatusCode, responseBody));
        }

        private async Task<(int? id, string? slug)> TryUpdateProductAttributeAsync(string baseUrl, int wooAttrId, object wooAttrData, HttpClient httpClient, CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/attributes/{wooAttrId}";
            var updateJson = JsonSerializer.Serialize(wooAttrData);
            using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
            var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
            var responseBody = await updateResponse.Content.ReadAsStringAsync(cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return (null, null);

            var updated = TryDeserializeFromResponse<WooCommerceAttributeResponse>(responseBody, updateUrl, "PUT");
            return updated != null ? (updated.id, updated.slug) : (null, null);
        }

        /// <summary>Per-request timeout for attribute term POST so a single slow/hanging request doesn't block sync (default HttpClient timeout is 30 min).</summary>
        private static readonly TimeSpan AttributeTermRequestTimeout = TimeSpan.FromSeconds(220);

        private async Task SyncAttributeTermsAsync(string baseUrl, int wooAttrId, Attribute attribute, HttpClient httpClient, CancellationToken cancelToken)
        {
            var values = attribute.AttributeValue?.Select(av => av.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? new List<string>();
            foreach (var value in values)
            {
                var name = value?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var termUrl = $"{baseUrl}/products/attributes/{wooAttrId}/terms";
                var termPayload = new WooCommerceAttributeTermPayload { name = name };
                var opts = new JsonSerializerOptions { PropertyNamingPolicy = null };
                var termBody = JsonSerializer.Serialize(termPayload, opts);
                _logger.LogInformation("WooCommerce attribute term POST body: {Body}", termBody);
                using var termContent = new StringContent(termBody, Encoding.UTF8, "application/json");

                HttpResponseMessage? termRes = null;
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);
                    cts.CancelAfter(AttributeTermRequestTimeout);
                    termRes = await httpClient.PostAsync(termUrl, termContent, cts.Token);
                }
                catch (OperationCanceledException) when (!cancelToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Attribute term request timed out after {Seconds}s, skipping remaining terms for this attribute.", AttributeTermRequestTimeout.TotalSeconds);
                    break;
                }

                if (termRes == null)
                    continue;

                using (termRes)
                {
                    var err = termRes.IsSuccessStatusCode ? null : await termRes.Content.ReadAsStringAsync(cancelToken);
                    if (!termRes.IsSuccessStatusCode)
                    {
                        var wooErr = TryDeserialize<WooErrorResponse>(err ?? "{}");

                        if (wooErr?.code == "term_exists")
                            continue; // OK

                        _logger.LogWarning("Failed to create attribute term. Request body was: {Body}. WooCommerce error: {Error}", termBody, err);

                        if (wooErr?.code == "rest_missing_callback_param")
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Ensures a global attribute exists in the database and is synced to WooCommerce.
        /// Returns (WooCommerce attribute ID, taxonomy slug). Slug is the actual slug from WooCommerce (e.g. pa_xxx for Hebrew names).
        /// Uses a per-(siteId, attributeName) lock so parallel product syncs don't create the same attribute multiple times.
        /// </summary>
        private async Task<(int? id, string? slug)> EnsureGlobalAttributeAsync(
            string baseUrl,
            string attributeName,
            List<string> attributeValues,
            int siteId,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var lockKey = $"{siteId}:{NormalizeOptionKey(attributeName)}";
            var sem = AttributeEnsureLocks.GetOrAdd(lockKey, _ => new SemaphoreSlim(1, 1));
            var acquired = false;
            // Timeout so one slow attribute doesn't block the whole sync; 90s is enough for one attribute sync
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(90));
                try
                {
                    await sem.WaitAsync(cts.Token);
                    acquired = true;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancelToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Timeout waiting for attribute lock {LockKey}; proceeding without lock (possible duplicate attribute).", lockKey);
                }
            }
            try
            {
                // First, try to find existing Attribute in DB by name and siteId
                var filter = new AttributeFilter { Name = attributeName };
                var paging = new PagingExDto { Skip = 0, Take = 10, IncludeTotal = false };
                var existingAttributes = await _attributeStorage.GetAttributesAsync(filter, paging, cancelToken);
                var existingAttribute = existingAttributes.Items.FirstOrDefault(a =>
                    a.SiteId == siteId &&
                    string.Equals(a.Name, attributeName, StringComparison.OrdinalIgnoreCase) &&
                    !a.IsDeleted);

                Attribute attribute;
                if (existingAttribute != null)
                {
                    attribute = existingAttribute;
                }
                else
                {
                    // Create new Attribute in DB
                    attribute = await _attributeStorage.CreateAttributeAsync(
                        new Attribute
                        {
                            Name = attributeName,
                            SiteId = siteId,
                            CreationTime = DateTime.UtcNow,
                            GuidId = Guid.NewGuid()
                        },
                        attributeValues,
                        cancelToken);
                }

                // Sync to WooCommerce; get actual (id, slug) from API
                var (wooCommerceId, wooSlug) = await SyncAttributeAsync(baseUrl, attribute, httpClient, cancelToken);

                if (wooCommerceId.HasValue && attribute.WooCommerceId != wooCommerceId.Value)
                {
                    await _attributeStorage.UpdateAttributeWooCommerceIdAsync(attribute.Id, wooCommerceId.Value, cancelToken);
                }

                return (wooCommerceId, wooSlug);
            }
            finally
            {
                if (acquired)
                    sem.Release();
            }
        }

        private async Task<int?> SyncCategoryAsync(
    string baseUrl,
    Category category,
    int? parentWooId,
    HttpClient httpClient,
    CancellationToken cancelToken)
        {
            var wooCatData = new
            {
                name = category.Name,
                description = category.Description ?? "",
                parent = parentWooId ?? 0
                // ?????????: slug = Slugify(category.Name)
            };

            // 1) ?? ?? ??? WooCommerceId - ???? update
            if (category.WooCommerceId.HasValue)
            {
                var updatedId = await TryUpdateCategoryAsync(baseUrl, category.WooCommerceId.Value, wooCatData, httpClient, cancelToken);
                if (updatedId.HasValue) return updatedId.Value;
            }

            // 2) ???? ???? create
            var createUrl = $"{baseUrl}/products/categories";
            var createJson = JsonSerializer.Serialize(wooCatData);
            using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

            var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
            var responseBody = await createResponse.Content.ReadAsStringAsync(cancelToken);

            // Success
            if (createResponse.IsSuccessStatusCode)
            {
                var created = TryDeserializeFromResponse<WooCommerceCategoryResponse>(responseBody, createUrl, "POST");
                return created?.id;
            }

            // Error -> read body (already in responseBody)
            var errorBody = responseBody;

            // term_exists -> return resource_id (and optionally update it)
            var wooErr = TryDeserialize<WooErrorResponse>(errorBody);
            if (wooErr?.code == "term_exists" && wooErr.data?.resource_id is int existingId)
            {
                // ?????????: ????? ??existing category ?? description ???'
                var updatedId = await TryUpdateCategoryAsync(baseUrl, existingId, wooCatData, httpClient, cancelToken);
                return updatedId ?? existingId;
            }

            throw new Exception(GetUserFriendlyWooCommerceError((int)createResponse.StatusCode, errorBody));
        }

        private async Task<int?> TryUpdateCategoryAsync(
            string baseUrl,
            int wooCategoryId,
            object wooCatData,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/categories/{wooCategoryId}";
            var updateJson = JsonSerializer.Serialize(wooCatData);
            using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

            var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
            var responseBody = await updateResponse.Content.ReadAsStringAsync(cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return null;

            var updated = TryDeserializeFromResponse<WooCommerceCategoryResponse>(responseBody, updateUrl, "PUT");
            return updated?.id;
        }

        /// <summary>
        /// Deletes a product category from WooCommerce for the given site.
        /// </summary>
        public async Task<bool> DeleteCategoryFromWooCommerceAsync(int siteId, int wooCategoryId, CancellationToken cancelToken)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null || site.WooCommerceEnabled != true ||
                string.IsNullOrEmpty(site.WooCommerceUrl) ||
                string.IsNullOrEmpty(site.WooCommerceKey) ||
                string.IsNullOrEmpty(site.WooCommerceSecret))
                return false;

            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = WooCommerceHttpTimeout;
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

            var deleteUrl = $"{baseUrl}/products/categories/{wooCategoryId}?force=true";
            var deleteResponse = await httpClient.DeleteAsync(deleteUrl, cancelToken);
            if (deleteResponse.IsSuccessStatusCode)
                return true;
            var errorContent = await deleteResponse.Content.ReadAsStringAsync(cancelToken);
            _logger.LogWarning("Failed to delete category {WooCategoryId} from WooCommerce for site {SiteId}: {Error}", wooCategoryId, siteId, errorContent);
            return false;
        }

        /// <summary>
        /// Deserializes JSON from WooCommerce API response. If the response is HTML (e.g. error page, redirect)
        /// instead of JSON, throws a clear exception to avoid JsonException on '&lt;' invalid start.
        /// </summary>
        private static T? TryDeserializeFromResponse<T>(string responseBody, string requestUrl, string method)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return default;
            var trimmed = responseBody.TrimStart();
            if (trimmed.StartsWith("<"))
                throw new Exception($"WooCommerce returned HTML instead of JSON for {method} {requestUrl}. Check that the WooCommerce URL points to the site root and the REST API is enabled. Response starts with: {(responseBody.Length > 120 ? responseBody.Substring(0, 120) + "..." : responseBody)}");
            try
            {
                return JsonSerializer.Deserialize<T>(responseBody);
            }
            catch (JsonException ex)
            {
                throw new Exception($"WooCommerce response is not valid JSON for {method} {requestUrl}. {ex.Message}. Response starts with: {(responseBody.Length > 150 ? responseBody.Substring(0, 150) + "..." : responseBody)}", ex);
            }
        }

        private static T? TryDeserialize<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json); }
            catch { return default; }
        }

        /// <summary>
        /// Parses WooCommerce REST API error response and returns a user-friendly message (Hebrew when applicable).
        /// </summary>
        private static string GetUserFriendlyWooCommerceError(int statusCode, string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return "שגיאת WooCommerce (ללא פרטים).";

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                string? message = null;
                string? code = null;
                if (root.TryGetProperty("message", out var msgEl))
                    message = msgEl.GetString()?.Trim();
                if (root.TryGetProperty("code", out var codeEl))
                    code = codeEl.GetString();

                // WooCommerce often returns Hebrew in "message" - use it as-is
                if (!string.IsNullOrWhiteSpace(message))
                    return message;

                return code switch
                {
                    "product_invalid_sku" => "מק\"ט לא תקף או כפול. וודא שהמק\"ט ייחודי בכל החנות (כולל מוצרים ווריאציות).",
                    "product_invalid_data" => "נתוני מוצר לא תקינים.",
                    "woocommerce_rest_product_invalid_id" => "מזהה מוצר לא תקין.",
                    "woocommerce_rest_term_invalid" => "ערך תכונה או קטגוריה לא תקין.",
                    _ => $"שגיאת WooCommerce (קוד {statusCode})."
                };
            }
            catch
            {
                var preview = responseBody.Length > 200 ? responseBody.Substring(0, 200) + "…" : responseBody;
                return $"שגיאת WooCommerce (קוד {statusCode}): {preview}";
            }
        }

        private async Task<List<WooCommerceSyncResult>> SyncProductsAsync(
            string baseUrl,
            int siteId,
            List<int>? productIds,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken,
            IProgress<WooCommerceSyncProgress>? progress = null)
        {
            var results = new List<WooCommerceSyncResult>();

            // Resolve list of product IDs to sync (no full load here – each product is loaded inside its own scope when syncing)
            List<int> idsToSync;
            if (productIds != null && productIds.Any())
                idsToSync = productIds.Distinct().ToList();
            else
                idsToSync = await _productStorage.GetProductIdsForSiteAsync(siteId, cancelToken);

            _logger.LogInformation("WooCommerce sync: products sync started for site {SiteId}, {Count} products", siteId, idsToSync.Count);
            progress?.Report(new WooCommerceSyncProgress { Total = idsToSync.Count, Completed = 0, Failed = 0 });

            // Each product: load by ID and sync in the same scope so options/variants/weight are always complete (no detached-entity issues)
            const int batchSize = 16;
            for (int i = 0; i < idsToSync.Count; i += batchSize)
            {
                cancelToken.ThrowIfCancellationRequested();
                var batchIds = idsToSync.Skip(i).Take(batchSize).ToList();
                var batchTasks = batchIds.Select(async productId =>
                {
                    using var scope = _scopeFactory.CreateScope();
                    var wooService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                    return await wooService.SyncSingleProductByIdAsync(baseUrl, siteId, productId, categoryMap, httpClient, cancelToken);
                });
                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);
                progress?.Report(new WooCommerceSyncProgress
                {
                    Total = idsToSync.Count,
                    Completed = results.Count,
                    Failed = results.Count(r => !r.Success)
                });
            }

            var failed = results.Count(r => !r.Success);
            if (failed > 0)
                _logger.LogWarning("WooCommerce sync: products batch completed for site {SiteId}, {Success} succeeded, {Failed} failed", siteId, results.Count - failed, failed);
            else
                _logger.LogInformation("WooCommerce sync: products batch completed for site {SiteId}, all {Count} succeeded", siteId, results.Count);
            return results;
        }

        /// <summary>Loads the product by ID in the current scope (full options/variants/weight) and syncs to WooCommerce. Use this so stream sync always has complete data.</summary>
        public async Task<WooCommerceSyncResult> SyncSingleProductByIdAsync(
            string baseUrl,
            int siteId,
            int productId,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var product = await _productStorage.GetProductAsync(productId, cancelToken);
            if (product == null || !product.Site.Any(s => s.Id == siteId))
                return new WooCommerceSyncResult { Success = false, ProductId = productId, ProductName = "", Error = "Product not found or not in site." };
            return await SyncProductAsync(baseUrl, siteId, product, categoryMap, httpClient, cancelToken);
        }

        /// <summary>Syncs a single product to WooCommerce. Public so it can be invoked from a scoped WooCommerceService (each scope has its own DbContext).</summary>
        public async Task<WooCommerceSyncResult> SyncSingleProductAsync(
            string baseUrl,
            int siteId,
            Product product,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            return await SyncProductAsync(baseUrl, siteId, product, categoryMap, httpClient, cancelToken);
        }

        private async Task<WooCommerceSyncResult> SyncProductAsync(
            string baseUrl,
            int siteId,
            Product product,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            try
            {
                // Map stock status
                var stockStatus = "instock";
                if (product.StockStatus?.Name == "out_of_stock" || product.Status?.Name == "outOfStock")
                    stockStatus = "outofstock";
                else if (product.StockStatus?.Name == "on_backorder")
                    stockStatus = "onbackorder";

                // Map visibility
                var catalogVisibility = "visible";
                if (product.Visibility?.Name == "hidden" || product.Status?.Name == "hidden")
                    catalogVisibility = "hidden";
                else if (product.Visibility?.Name == "outOfStock")
                    catalogVisibility = "visible"; // Still visible even if out of stock

                // Map WooCommerce status
                var wooStatus = "publish";
                if (product.Status?.Name == "hidden")
                    wooStatus = "private";
                else if (product.Status?.Name == "outOfStock")
                    wooStatus = "publish";
                else if (product.Status?.Name == "active")
                    wooStatus = "publish";
                else if (product.Status?.Name == "draft")
                    wooStatus = "draft";
                else if (product.Status?.Name == "archived")
                    wooStatus = "private";

                // Map shipping class
                var shippingClass = "";
                if (product.ShippingClass?.Name == "heavy")
                    shippingClass = "heavy";
                else if (product.ShippingClass?.Name == "fragile")
                    shippingClass = "fragile";

                // Map categories
                var allCategoryIds = product.ProductCategory?
                    .Select(pc => pc.CategoryId)
                    .Where(id => categoryMap.ContainsKey(id))
                    .Select(id => new { id = categoryMap[id] })
                    .Cast<object>()
                    .ToList() ?? new List<object>();

                // Resolve WooCommerce product ID for update (so we can fetch existing images and use id to avoid duplicating).
                // Use site-scoped SKU so the same SKU in different branches (sites) does not collide when syncing to one WooCommerce store.
                int? existingWooId = product.WooCommerceId;
                if (!existingWooId.HasValue && !string.IsNullOrWhiteSpace(product.Sku))
                    existingWooId = await FindProductIdBySkuAsync(baseUrl, siteId, product.Sku, httpClient, cancelToken);

                List<(int id, string? src, string? name)>? existingWooImages = null;
                if (existingWooId.HasValue)
                    existingWooImages = await GetWooCommerceProductImagesAsync(baseUrl, existingWooId.Value, httpClient, cancelToken);

                // Map images: when updating, use existing WooCommerce image id when URL matches to avoid duplicating in media library.
                // Use the image name from the system (Media.Name) so WooCommerce gets a friendly filename instead of the long URL.
                var ourProductImages = product.ProductImage?
                    .OrderBy(pi => pi.SortOrder)
                    .Where(pi => IsPublicImageUrl(pi.Url))
                    .ToList() ?? new List<ProductImage>();
                var images = new List<object>();
                for (var i = 0; i < ourProductImages.Count; i++)
                {
                    var pi = ourProductImages[i];
                    var url = pi.Url?.Trim() ?? "";
                    var position = i;
                    var friendlyName = pi.Media?.Name?.Trim();
                    if (string.IsNullOrEmpty(friendlyName) && !string.IsNullOrEmpty(url))
                    {
                        try
                        {
                            var lastSegment = new Uri(url, UriKind.RelativeOrAbsolute).Segments.LastOrDefault()?.Trim('/');
                            if (!string.IsNullOrEmpty(lastSegment))
                                friendlyName = lastSegment;
                        }
                        catch { /* ignore URI parse */ }
                    }
                    if (existingWooImages != null)
                    {
                        // Match by name first (system/WooCommerce image name), then by URL
                        var match = default((int id, string? src, string? name));
                        if (!string.IsNullOrEmpty(friendlyName))
                            match = existingWooImages.FirstOrDefault(ex => string.Equals((ex.name ?? "").Trim(), friendlyName, StringComparison.OrdinalIgnoreCase));
                        if (match.id == 0)
                            match = existingWooImages.FirstOrDefault(ex => string.Equals((ex.src ?? "").Trim(), url, StringComparison.OrdinalIgnoreCase));
                        if (match.id != 0)
                            images.Add(new { id = match.id, position });
                        else
                            images.Add(string.IsNullOrEmpty(friendlyName) ? (object)new { src = url, position } : new { src = url, name = friendlyName, position });
                    }
                    else
                        images.Add(string.IsNullOrEmpty(friendlyName) ? (object)new { src = url, position } : new { src = url, name = friendlyName, position });
                }

                // Map tags
                var tags = product.Tag?
                    .Select(t => new { name = t.Name })
                    .Cast<object>()
                    .ToList() ?? new List<object>();

                // Build meta data
                var metaData = new List<object>();
                if (!string.IsNullOrEmpty(product.Brand?.Name))
                    metaData.Add(new { key = "_brand", value = product.Brand.Name });
                if (!string.IsNullOrEmpty(product.Supplier?.Name))
                    metaData.Add(new { key = "_supplier", value = product.Supplier.Name });
                metaData.Add(new { key = "_is_kosher", value = product.IsKosher == true ? "yes" : "no" });
                var showAsMl = product.ShowAsMl == true || product.WeightUnit == "ml";
                metaData.Add(new { key = "_show_as_ml", value = showAsMl ? "yes" : "no" });
                metaData.Add(new { key = "show_as_ml_", value = showAsMl ? "yes" : "no" });
                if (product.CostPrice.HasValue)
                    metaData.Add(new { key = "_cost_price", value = product.CostPrice.Value.ToString() });
                if (!string.IsNullOrEmpty(product.SeoTitle))
                    metaData.Add(new { key = "_yoast_wpseo_title", value = product.SeoTitle });
                if (!string.IsNullOrEmpty(product.SeoDescription))
                    metaData.Add(new { key = "_yoast_wpseo_metadesc", value = product.SeoDescription });

                // Weighted product fields - "זה מוצר שקיל". Keys must match WordPress admin POST (post.php): leading _ and no trailing _.
                // When IsWeighted is null, derive from SetupType (same as frontend edit form).
                var setupTypeName = product.SetupType?.Name ?? "";
                var isWeightedBySetup = setupTypeName is "by_weight" or "by_unit" or "by_unit_and_weight";
                var isWeighted = product.IsWeighted == true || (product.IsWeighted != false && isWeightedBySetup);
                var weighableValue = isWeighted ? "yes" : "no";
                metaData.Add(new { key = "_ocwsu_weighable", value = weighableValue });
                metaData.Add(new { key = "ocwsu_weighable_", value = weighableValue });
                metaData.Add(new { key = "ocwsu_weightable", value = weighableValue });

                if (isWeighted)
                {
                    var soldByUnits = (setupTypeName == "by_unit" || setupTypeName == "by_unit_and_weight") ? "yes" : "no";
                    var soldByWeight = (setupTypeName == "by_weight" || setupTypeName == "by_unit_and_weight") ? "yes" : "no";
                    metaData.Add(new { key = "_ocwsu_sold_by_units", value = soldByUnits });
                    metaData.Add(new { key = "ocwsu_sold_by_units_", value = soldByUnits });
                    metaData.Add(new { key = "_ocwsu_sold_by_weight", value = soldByWeight });
                    metaData.Add(new { key = "ocwsu_sold_by_weight_", value = soldByWeight });

                    if (product.WeightConfig != null)
                    {
                        var weightConfig = product.WeightConfig;
                        metaData.Add(new { key = "_ocwsu_product_weight_units", value = weightConfig.Unit?.Name ?? "" });
                        metaData.Add(new { key = "ocwsu_product_weight_units_", value = weightConfig.Unit?.Name ?? "" });
                        metaData.Add(new { key = "_ocwsu_display_price_per_100g", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        metaData.Add(new { key = "ocwsu_display_price_per_100g_", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        metaData.Add(new { key = "_ocwsu_min_weight", value = weightConfig.StartWeight ?? "" });
                        metaData.Add(new { key = "ocwsu_min_weight_", value = weightConfig.StartWeight ?? "" });
                        metaData.Add(new { key = "_ocwsu_weight_step", value = weightConfig.Step ?? "" });
                        metaData.Add(new { key = "ocwsu_weight_step_", value = weightConfig.Step ?? "" });
                        // WooCommerce plugin expects "fixed" for משקל קבוע; we store "average" for that mode
                        var unitWeightTypeForWoo = string.Equals(weightConfig.UnitWeightMode?.Name, "average", StringComparison.OrdinalIgnoreCase) ? "fixed" : (weightConfig.UnitWeightMode?.Name ?? "");
                        metaData.Add(new { key = "_ocwsu_unit_weight_type", value = unitWeightTypeForWoo });
                        metaData.Add(new { key = "ocwsu_unit_weight_type_", value = unitWeightTypeForWoo });
                        metaData.Add(new { key = "_ocwsu_unit_weight", value = weightConfig.UnitWeight ?? "" });
                        metaData.Add(new { key = "ocwsu_unit_weight_", value = weightConfig.UnitWeight ?? "" });
                        // WooCommerce expects one weight option per line; we store comma-separated
                        var weightOptionsRaw = weightConfig.WeightOptions ?? "";
                        var unitWeightOptionsForWoo = string.IsNullOrWhiteSpace(weightOptionsRaw)
                            ? ""
                            : string.Join("\n", weightOptionsRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0));
                        metaData.Add(new { key = "_ocwsu_unit_weight_options", value = unitWeightOptionsForWoo });
                        metaData.Add(new { key = "ocwsu_unit_weight_options_", value = unitWeightOptionsForWoo });
                        var getWeightFromVariation = weightConfig.WeightByVariant == true ? "yes" : "no";
                        metaData.Add(new { key = "_ocwsu_get_weight_from_variation", value = getWeightFromVariation });
                        metaData.Add(new { key = "ocwsu_get_weight_from_variation_", value = getWeightFromVariation });
                    }
                }

                var wooSku = GetWooCommerceSku(siteId, product.Sku);
                // Send product weight to WooCommerce only when product is weighable; otherwise leave empty so previous value is not applied.
                // Fallback: for weighted setups where Product.Weight is empty, use WeightConfig.UnitWeight.
                //var productWeightForWoo = ResolveProductWeightForWoo(product, isWeighted);
                var productWeightForWoo = product.Weight.HasValue && product.Weight.Value > 0 ? (product.Weight?.ToString() ?? "") : "";
                var wooProduct = new Dictionary<string, object>
                {
                    ["name"] = product.Name,
                    ["type"] = (product.ProductVariant != null && product.ProductVariant.Any(v => !v.IsDeleted)) ? "variable" : "simple",
                    ["description"] = product.LongDescription ?? "",
                    ["short_description"] = product.ShortDescription ?? "",
                    ["sku"] = wooSku,
                    ["catalog_visibility"] = catalogVisibility,
                    ["weight"] = productWeightForWoo,
                    ["shipping_class"] = shippingClass,
                    ["menu_order"] = product.DisplayOrder ?? 0,
                    ["images"] = images,
                    ["categories"] = allCategoryIds,
                    ["tags"] = tags,
                    ["status"] = wooStatus,
                    ["meta_data"] = metaData
                };

                // For variable products, ensure global attributes exist and use their IDs + actual slugs from WooCommerce
                var attributeMap = new Dictionary<string, int?>();   // attribute name -> WooCommerce ID
                var attributeSlugMap = new Dictionary<string, string>(); // attribute name -> WooCommerce taxonomy slug (e.g. pa_xxx)

                // For simple products, add pricing and stock and clear attributes (so WooCommerce removes variation attributes when product was variable before)
                if (product.ProductVariant == null || !product.ProductVariant.Any(v => !v.IsDeleted))
                {
                    wooProduct["attributes"] = new List<object>();
                    wooProduct["regular_price"] = product.Price?.ToString() ?? "0";
                    // WooCommerce rejects empty string for sale_price and date fields; use null when no value (avoids 400 Bad Request on update)
                    wooProduct["sale_price"] = product.SalePrice.HasValue ? product.SalePrice.Value.ToString() : (object?)null;
                    wooProduct["date_on_sale_from"] = product.SalePriceStartDate.HasValue ? product.SalePriceStartDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : (object?)null;
                    wooProduct["date_on_sale_to"] = product.SalePriceEndDate.HasValue ? product.SalePriceEndDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : (object?)null;
                    wooProduct["manage_stock"] = product.StockManagementType?.Name == "quantity";
                    wooProduct["stock_quantity"] = product.StockQuantity ?? 0;
                    wooProduct["stock_status"] = stockStatus;
                    wooProduct["backorders"] = product.StockStatus?.Name == "on_backorder" ? "yes" : "no";
                }
                else
                {
                    // For variable products, ensure global attributes exist and use their IDs
                    
                    if (product.ProductOption != null)
                    {
                        foreach (var option in product.ProductOption.Where(po => !po.IsDeleted))
                        {
                            var attributeValues = option.ProductOptionValue?
                                .Select(pov => pov.Value)
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .ToList() ?? new List<string>();

                            if (attributeValues.Any())
                            {
                                // Ensure global attribute exists in DB and WooCommerce; get actual (id, slug) from API
                                var (wooAttrId, wooSlug) = await EnsureGlobalAttributeAsync(
                                    baseUrl,
                                    option.Name,
                                    attributeValues,
                                    siteId,
                                    httpClient,
                                    cancelToken);
                                
                                if (wooAttrId.HasValue)
                                {
                                    var key = NormalizeOptionKey(option.Name);
                                    attributeMap[key] = wooAttrId.Value;
                                    if (!string.IsNullOrEmpty(wooSlug))
                                        attributeSlugMap[key] = wooSlug;
                                }
                            }
                        }
                    }

                    // Build attributes array using global attribute IDs. Include "name" (taxonomy slug) so WooCommerce
                    // reliably relates attributes to the product; options remain display names for the dropdown.
                    var attributes = product.ProductOption?
                        .Where(po => !po.IsDeleted && attributeMap.ContainsKey(NormalizeOptionKey(po.Name)))
                        .Select((option, index) =>
                        {
                            var key = NormalizeOptionKey(option.Name);
                            var attrId = attributeMap[key]!.Value;
                            var slug = attributeSlugMap.TryGetValue(key, out var s) ? s : null;
                            var dict = new Dictionary<string, object>
                            {
                                ["id"] = attrId,
                                ["position"] = index,
                                ["visible"] = true,
                                ["variation"] = true,
                                ["options"] = option.ProductOptionValue?
                                    .Select(pov => pov.Value)
                                    .Where(v => !string.IsNullOrWhiteSpace(v))
                                    .Select(v => v.Trim())
                                    .Distinct()
                                    .ToList() ?? new List<string>()
                            };
                            if (!string.IsNullOrEmpty(slug))
                                dict["name"] = slug;
                            return dict;
                        })
                        .Cast<object>()
                        .ToList() ?? new List<object>();

                    wooProduct["attributes"] = attributes;


                }

                // Create or update product
                int? wooCommerceId = null;
                string action = "created";

                if (existingWooId.HasValue)
                {
                    // Update existing product (existingWooId was resolved above for image deduplication)
                    var updateUrl = $"{baseUrl}/products/{existingWooId.Value}";
                    var updateJson = JsonSerializer.Serialize(wooProduct);
                    var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                    var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
                    if (updateResponse.IsSuccessStatusCode)
                    {
                        var updated = await JsonSerializer.DeserializeAsync<WooCommerceProductResponse>(
                            await updateResponse.Content.ReadAsStreamAsync(cancelToken),
                            cancellationToken: cancelToken);
                        wooCommerceId = updated?.id;
                        action = "updated";
                    }
                    else
                    {
                        // If update fails, try to create
                        var errorContent = await updateResponse.Content.ReadAsStringAsync(cancelToken);
                        _logger.LogWarning("Failed to update product {ProductId} in WooCommerce: {Error}", product.Id, errorContent);
                    }
                }

                if (!wooCommerceId.HasValue)
                {
                    // Create new product
                    var createUrl = $"{baseUrl}/products";
                    var createJson = JsonSerializer.Serialize(wooProduct);
                    var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

                    var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
                    if (!createResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await createResponse.Content.ReadAsStringAsync(cancelToken);
                        throw new Exception(GetUserFriendlyWooCommerceError((int)createResponse.StatusCode, errorContent));
                    }

                    var created = await JsonSerializer.DeserializeAsync<WooCommerceProductResponse>(
                        await createResponse.Content.ReadAsStreamAsync(cancelToken),
                        cancellationToken: cancelToken);
                    wooCommerceId = created?.id;
                }

                // Update product with WooCommerce ID
                if (wooCommerceId.HasValue && product.WooCommerceId != wooCommerceId.Value)
                {
                    await _productStorage.UpdateProductWooCommerceIdAsync(product.Id, wooCommerceId.Value, cancelToken);
                }

                // Apply menu_order: send a dedicated PUT so WooCommerce reliably persists it (main payload may not include/apply it in all versions)
                if (wooCommerceId.HasValue)
                {
                    try
                    {
                        await UpdateWooCommerceProductMenuOrderAsync(baseUrl, wooCommerceId.Value, product.DisplayOrder ?? 0, httpClient, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "WooCommerce menu_order update failed for product {ProductId} (WooCommerce id {WooId}); product sync succeeded.", product.Id, wooCommerceId.Value);
                    }
                }

                // Sync variations for variable products
                if (wooCommerceId.HasValue && product.ProductVariant != null && product.ProductVariant.Any(v => !v.IsDeleted))
                {
                    await SyncProductVariantsAsync(baseUrl, siteId, wooCommerceId.Value, product, attributeMap, attributeSlugMap, httpClient, cancelToken);
                }

                // Only count as success when we actually got a WooCommerce ID (created or updated)
                var isSuccess = wooCommerceId.HasValue;
                if (!isSuccess)
                    _logger.LogWarning("WooCommerce sync product failed: ProductId={ProductId}, Name={ProductName}, no WooCommerce ID returned (create/update may have failed)", product.Id, product.Name ?? "");
                return new WooCommerceSyncResult
                {
                    Success = isSuccess,
                    ProductId = product.Id,
                    ProductName = product.Name ?? "",
                    WooCommerceId = wooCommerceId,
                    Action = action,
                    Error = isSuccess ? null : "No WooCommerce ID returned (create/update may have failed)."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync product {ProductId}, Name={ProductName}, Error={Error}", product.Id, product.Name ?? "", ex.Message);
                return new WooCommerceSyncResult
                {
                    Success = false,
                    ProductId = product.Id,
                    ProductName = product.Name ?? "",
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Updates the order status on the store side (WooCommerce or oc-storeos). When base URL is oc-storeos, POSTs full order to their API (order_id + status + customer + items etc.). Otherwise PUTs status to WooCommerce REST API. Does not throw; logs errors.
        /// </summary>
        public async Task UpdateOrderStatusAsync(int siteId, string wooOrderId, string status, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(wooOrderId)) return;
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null || site.WooCommerceEnabled != true) return;
            var baseUrl = !string.IsNullOrWhiteSpace(site.WooCommerceOrderUpdateBaseUrl)
                ? site.WooCommerceOrderUpdateBaseUrl.Trim().TrimEnd('/')
                : null;
            var isOcStoreos = !string.IsNullOrEmpty(baseUrl) && baseUrl.Contains("oc-storeos", StringComparison.OrdinalIgnoreCase);
            if (isOcStoreos)
            {
                var order = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, wooOrderId.Trim(), cancelToken);
                if (order != null)
                    await SyncOrderToOcStoreosAsync(siteId, order.Id, cancelToken);
                return;
            }
            if (string.IsNullOrEmpty(site.WooCommerceKey) || string.IsNullOrEmpty(site.WooCommerceSecret)) return;
            if (string.IsNullOrEmpty(site.WooCommerceUrl)) return;
            baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");
            var url = $"{baseUrl}/orders/{wooOrderId.Trim()}";
            var body = JsonSerializer.Serialize(new { status });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync(url, content, cancelToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancelToken);
                _logger.LogWarning("WooCommerce order status update failed for site {SiteId}, order {WooOrderId}: {Status} {Error}", siteId, wooOrderId, (int)response.StatusCode, err);
            }
        }

        /// <summary>
        /// Syncs full order to oc-storeos API (POST .../orders). Used when completing/updating/cancelling orders that came from WooCommerce. Payload: orderId, status, externalOrderId, customer, shippingAddress, shippingInfo, items (sku/quantity), shippingTotal, customerNotes. Does not throw; logs errors.
        /// </summary>
        public async Task SyncOrderToOcStoreosAsync(int siteId, int orderId, CancellationToken cancelToken)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null || string.IsNullOrWhiteSpace(site.WooCommerceOrderUpdateBaseUrl)) return;
            var baseUrl = site.WooCommerceOrderUpdateBaseUrl.Trim().TrimEnd('/');
            if (!baseUrl.Contains("oc-storeos", StringComparison.OrdinalIgnoreCase)) return;
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null || string.IsNullOrWhiteSpace(order.ExternalOrderId)) return;
            var wcStatus = MapOurStatusToWooCommerceForOcStoreos(order.Status);
            var productIds = (order.OrderItem ?? new List<OrderItem>()).Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
            var productSkuAndWoo = new Dictionary<int, (string? Sku, int? WooCommerceId)>();
            if (productIds.Count > 0)
            {
                var paging = new PagingExDto { Skip = 0, Take = productIds.Count + 10, IncludeTotal = false };
                var productsRes = await _productStorage.GetProductsBySiteAndIdsAsync(siteId, productIds, paging, cancelToken);
                foreach (var p in productsRes.Items ?? Enumerable.Empty<Product>())
                    productSkuAndWoo[p.Id] = (p.Sku, p.WooCommerceId);
            }
            var items = new List<object>();
            foreach (var line in order.OrderItem?.OrderBy(i => i.SortOrder) ?? Enumerable.Empty<OrderItem>())
            {
                var qty = (decimal)(line.Quantity > 0 ? line.Quantity : 1);
                if (line.ProductId.HasValue && productSkuAndWoo.TryGetValue(line.ProductId.Value, out var skuWoo))
                {
                    if (!string.IsNullOrWhiteSpace(skuWoo.Sku))
                        items.Add(new { sku = skuWoo.Sku.Trim(), quantity = qty });
                    else if (skuWoo.WooCommerceId.HasValue)
                        items.Add(new { productId = skuWoo.WooCommerceId.Value, quantity = qty });
                    else
                        items.Add(new { quantity = qty });
                }
                else
                    items.Add(new { quantity = qty });
            }
            var deliveryDate = order.DeliveryDate ?? order.PickupDate;
            var deliveryTime = order.DeliveryTime ?? order.PickupTime;
            var slotStart = string.IsNullOrWhiteSpace(deliveryTime) ? (string?)null : deliveryTime.Trim();
            var deliveryType = (order.DeliveryType ?? "").Trim();
            var shippingType = string.Equals(deliveryType, "Pickup", StringComparison.OrdinalIgnoreCase) ? "pickup" : "delivery";
            var payload = new Dictionary<string, object?>
            {
                ["orderId"] = int.TryParse(order.ExternalOrderId, out var oid) ? oid : (object)order.ExternalOrderId,
                ["status"] = wcStatus ?? "on-hold",
                ["externalOrderId"] = order.OrderNumber ?? order.ExternalOrderId,
                ["customer"] = new Dictionary<string, string?>
                {
                    ["name"] = order.CustomerName,
                    ["email"] = order.CustomerEmail ?? "",
                    ["phone"] = order.CustomerPhone ?? ""
                },
                ["shippingAddress"] = new Dictionary<string, string?>
                {
                    ["street"] = order.DeliveryAddress,
                    ["city"] = "",
                    ["zip"] = ""
                },
                ["shippingInfo"] = new Dictionary<string, object?>
                {
                    ["type"] = shippingType,
                    ["date"] = deliveryDate.HasValue ? deliveryDate.Value.ToString("yyyy-MM-dd") : null,
                    ["slotStart"] = slotStart,
                    ["slotEnd"] = slotStart
                },
                ["items"] = items,
                ["shippingTotal"] = order.ShippingCost ?? 0,
                ["customerNotes"] = order.CustomerNote ?? ""
            };
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };
            var body = JsonSerializer.Serialize(payload, jsonOptions);
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var url = $"{baseUrl}/orders";
            var response = await httpClient.PostAsync(url, content, cancelToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancelToken);
                _logger.LogWarning("oc-storeos order sync failed for site {SiteId}, order {OrderId}: {Status} {Error}", siteId, orderId, (int)response.StatusCode, err);
            }
        }

        private static string MapOurStatusToWooCommerceForOcStoreos(string? ourStatus)
        {
            if (string.IsNullOrWhiteSpace(ourStatus)) return "on-hold";
            var s = ourStatus.Trim();
            if (string.Equals(s, "New", StringComparison.OrdinalIgnoreCase)) return "on-hold";
            if (string.Equals(s, "InTreatment", StringComparison.OrdinalIgnoreCase)) return "processing";
            if (string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase)) return "completed";
            if (string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase)) return "cancelled";
            return "on-hold";
        }

        /// <summary>
        /// Syncs only menu_order to WooCommerce for the given ordered product IDs (e.g. after reorder). Much faster than full product sync.
        /// </summary>
        public async Task SyncMenuOrderOnlyAsync(int siteId, List<int> orderedProductIds, CancellationToken cancelToken)
        {
            if (orderedProductIds == null || !orderedProductIds.Any()) return;
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null || string.IsNullOrEmpty(site.WooCommerceUrl) || string.IsNullOrEmpty(site.WooCommerceKey) || string.IsNullOrEmpty(site.WooCommerceSecret))
                return;
            var orders = await _productStorage.GetWooCommerceIdAndDisplayOrderForSiteAsync(orderedProductIds, siteId, cancelToken);
            if (orders.Count == 0) return;
            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");
            const int concurrency = 10;
            for (var i = 0; i < orders.Count; i += concurrency)
            {
                var batch = orders.Skip(i).Take(concurrency).ToList();
                var tasks = batch.Select(o => UpdateWooCommerceProductMenuOrderAsync(baseUrl, o.WooCommerceId, o.DisplayOrder, httpClient, cancelToken));
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Sends a PUT with only menu_order so WooCommerce persists sort order (main product payload may not include or apply menu_order in all setups).
        /// </summary>
        private static async Task UpdateWooCommerceProductMenuOrderAsync(
            string baseUrl,
            int wooProductId,
            int menuOrder,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/{wooProductId}";
            var body = JsonSerializer.Serialize(new { menu_order = menuOrder });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var response = await httpClient.PutAsync(updateUrl, content, cancelToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancelToken);
                throw new InvalidOperationException($"WooCommerce menu_order update failed ({(int)response.StatusCode}): {err}");
            }
        }

        private async Task SyncProductVariantsAsync(
            string baseUrl,
            int siteId,
            int wooProductId,
            Product product,
            Dictionary<string, int?> attributeMap,
            Dictionary<string, string> attributeSlugMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var variants = product.ProductVariant?.Where(v => !v.IsDeleted).ToList() ?? new List<ProductVariant>();

            // Fetch existing WooCommerce variations so we can match by attributes (avoid duplicates) and delete removed ones
            var existingWoo = await GetExistingWooCommerceVariationsAsync(baseUrl, wooProductId, httpClient, cancelToken);
            var existingIdsSet = existingWoo.Select(x => x.id).ToHashSet();
            // signature -> list of Woo variation ids (so we can match one per our variant and delete the rest as duplicates)
            var signatureToIds = existingWoo
                .GroupBy(x => x.signature ?? "")
                .ToDictionary(g => g.Key, g => g.Select(x => x.id).ToList());

            var usedWooVariationIds = new HashSet<int>();

            // When stock is not managed per variation ("במלאי" / "מלאי לפי כמות"), variations must not have their own stock column so they inherit from product.
            var stockManagedPerVariation = string.Equals(product.StockManagementType?.Name, "variation", StringComparison.OrdinalIgnoreCase);
            var productStockStatus = "instock";
            if (product.StockStatus?.Name == "out_of_stock" || product.Status?.Name == "outOfStock")
                productStockStatus = "outofstock";
            else if (product.StockStatus?.Name == "on_backorder")
                productStockStatus = "onbackorder";

            foreach (var variant in variants)
            {
                try
                {
                    var variantStockStatus = stockManagedPerVariation
                        ? ((variant.StockQuantity ?? 0) > 0 ? "instock" : "outofstock")
                        : productStockStatus;

                    var variantOptionValues = variant.ProductVariantOptionValue?
                        .Where(x => !string.IsNullOrWhiteSpace(x.OptionName))
                        .ToDictionary(
                            x => NormalizeOptionKey(x.OptionName),
                            x => (x.OptionValue ?? "").Trim()
                        ) ?? new Dictionary<string, string>();

                    var variationAttributesList = new List<object>();
                    foreach (var kvp in variantOptionValues)
                    {
                        if (!attributeMap.TryGetValue(kvp.Key, out var attrIdNullable) || !attrIdNullable.HasValue)
                            continue;
                        var attrId = attrIdNullable.Value;
                        variationAttributesList.Add(new Dictionary<string, object>
                        {
                            ["id"] = attrId,
                            ["option"] = kvp.Value
                        });
                    }

                    if (variationAttributesList.Count == 0)
                    {
                        _logger.LogWarning("Variation {VariantId} for product {ProductId} has no matching attributes (attributeMap or ProductVariantOptionValues). Skipping this variation.", variant.Id, product.Id);
                        continue;
                    }

                    var ourSignature = BuildOurVariationSignature(variantOptionValues, attributeMap);
                    int? wooVariationIdToUse = null;

                    // 1) Prefer our stored WooCommerce variation id if it still exists in WooCommerce
                    if (variant.WooCommerceVariationId.HasValue && existingIdsSet.Contains(variant.WooCommerceVariationId.Value))
                    {
                        wooVariationIdToUse = variant.WooCommerceVariationId.Value;
                        usedWooVariationIds.Add(wooVariationIdToUse.Value);
                    }
                    // 2) Else match by attribute signature (same combination = same variation; avoids creating duplicates)
                    else if (!string.IsNullOrEmpty(ourSignature) && signatureToIds.TryGetValue(ourSignature, out var idList) && idList.Count > 0)
                    {
                        var taken = idList[0];
                        idList.RemoveAt(0);
                        wooVariationIdToUse = taken;
                        usedWooVariationIds.Add(taken);
                    }

                    var variantWooSku = GetWooCommerceSku(siteId, variant.Sku);
                    // Variation weight only when product is weighable (same as product-level weight).
                    //var variationWeightForWoo = isWeighted ? (variant.Weight?.ToString() ?? "") : "";
                    var wooVariation = new Dictionary<string, object>
                    {
                        ["regular_price"] = variant.Price?.ToString() ?? product.Price?.ToString() ?? "0",
                        ["sale_price"] = variant.SalePrice?.ToString() ?? "",
                        ["sku"] = variantWooSku,
                        ["manage_stock"] = stockManagedPerVariation,
                        ["stock_status"] = variantStockStatus,
                        //["weight"] = variationWeightForWoo,
                        ["weight"] = variant.Weight?.ToString() ?? "",
                        ["attributes"] = variationAttributesList
                    };
                    if (stockManagedPerVariation)
                        wooVariation["stock_quantity"] = variant.StockQuantity ?? 0;
                    if (!string.IsNullOrEmpty(variant.ImageUrl))
                        wooVariation["image"] = new { src = variant.ImageUrl };

                    int? wooVariationId = null;

                    if (wooVariationIdToUse.HasValue)
                    {
                        var updateUrl = $"{baseUrl}/products/{wooProductId}/variations/{wooVariationIdToUse.Value}";
                        var updateJson = JsonSerializer.Serialize(wooVariation);
                        using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
                        var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
                        if (updateResponse.IsSuccessStatusCode)
                        {
                            var updated = await JsonSerializer.DeserializeAsync<WooCommerceVariationResponse>(
                                await updateResponse.Content.ReadAsStreamAsync(cancelToken),
                                cancellationToken: cancelToken);
                            wooVariationId = updated?.id;
                        }
                    }

                    if (!wooVariationId.HasValue)
                    {
                        var createUrl = $"{baseUrl}/products/{wooProductId}/variations";
                        var createJson = JsonSerializer.Serialize(wooVariation);
                        using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
                        var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
                        if (createResponse.IsSuccessStatusCode)
                        {
                            var created = await JsonSerializer.DeserializeAsync<WooCommerceVariationResponse>(
                                await createResponse.Content.ReadAsStreamAsync(cancelToken),
                                cancellationToken: cancelToken);
                            wooVariationId = created?.id;
                        }
                        else
                        {
                            var errorContent = await createResponse.Content.ReadAsStringAsync(cancelToken);
                            _logger.LogWarning("Failed to create variation for product {ProductId}: {Error}", product.Id, errorContent);
                        }
                    }

                    if (wooVariationId.HasValue && variant.WooCommerceVariationId != wooVariationId.Value)
                        await _productStorage.UpdateProductVariantWooCommerceIdAsync(variant.Id, wooVariationId.Value, cancelToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync variation {VariantId} for product {ProductId}", variant.Id, product.Id);
                }
            }

            // Delete WooCommerce variations that we no longer have (removed in our product) or duplicates (same signature).
            // Run deletes in parallel with limited concurrency to avoid blocking and to speed up.
            var toDelete = existingIdsSet.Where(id => !usedWooVariationIds.Contains(id)).ToList();
            const int deleteConcurrency = 5;
            for (var i = 0; i < toDelete.Count; i += deleteConcurrency)
            {
                var batch = toDelete.Skip(i).Take(deleteConcurrency).ToList();
                var tasks = batch.Select(async wooId =>
                {
                    try
                    {
                        var deleteUrl = $"{baseUrl}/products/{wooProductId}/variations/{wooId}?force=true";
                        var deleteResponse = await httpClient.DeleteAsync(deleteUrl, cancelToken);
                        if (!deleteResponse.IsSuccessStatusCode)
                            _logger.LogWarning("Failed to delete WooCommerce variation {WooId} for product {ProductId}: {Status}", wooId, product.Id, deleteResponse.StatusCode);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting WooCommerce variation {WooId} for product {ProductId}", wooId, product.Id);
                    }
                });
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Fetches existing variations for a WooCommerce variable product. Returns (id, signature) for each;
        /// signature is a stable key from attributes (attrId:option) for matching our variants to existing ones.
        /// Limited to MaxVariationFetchPages to avoid long stalls on products with many variations.
        /// </summary>
        private const int MaxVariationFetchPages = 5;
        private const int VariationsPerPage = 100;

        private static async Task<List<(int id, string signature)>> GetExistingWooCommerceVariationsAsync(
            string baseUrl,
            int wooProductId,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var result = new List<(int id, string signature)>();
            for (var page = 1; page <= MaxVariationFetchPages; page++)
            {
                var url = $"{baseUrl}/products/{wooProductId}/variations?per_page={VariationsPerPage}&page={page}";
                var response = await httpClient.GetAsync(url, cancelToken);
                if (!response.IsSuccessStatusCode) break;
                var body = await response.Content.ReadAsStringAsync(cancelToken);
                var list = TryDeserialize<List<WooCommerceVariationListItem>>(body);
                if (list == null || list.Count == 0) break;
                foreach (var v in list)
                {
                    var sig = BuildWooVariationSignature(v.attributes);
                    result.Add((v.id, sig));
                }
                if (list.Count < VariationsPerPage) break;
            }
            return result;
        }

        /// <summary>Builds a stable signature from WooCommerce variation attributes (id:option sorted by id).</summary>
        private static string BuildWooVariationSignature(List<WooCommerceVariationAttributeItem>? attributes)
        {
            if (attributes == null || attributes.Count == 0) return "";
            var parts = attributes
                .Where(a => a.id != 0)
                .Select(a => $"{a.id}:{(a.option ?? "").Trim()}")
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToList();
            return string.Join("|", parts);
        }

        /// <summary>Builds the same signature format from our variant option values and attributeMap.</summary>
        private static string BuildOurVariationSignature(
            Dictionary<string, string> variantOptionValues,
            Dictionary<string, int?> attributeMap)
        {
            if (variantOptionValues == null || attributeMap == null) return "";
            var parts = new List<string>();
            foreach (var kvp in variantOptionValues)
            {
                if (!attributeMap.TryGetValue(kvp.Key, out var idVal) || !idVal.HasValue) continue;
                var val = (kvp.Value ?? "").Trim();
                parts.Add($"{idVal.Value}:{val}");
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        /// <summary>
        /// Returns the SKU to use in WooCommerce. Synced without site prefix so the product SKU in WooCommerce matches the system SKU.
        /// </summary>
        private static string GetWooCommerceSku(int siteId, string? sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return "";
            return sku.Trim();
        }

        /// <summary>
        /// Finds a product in WooCommerce by SKU. Tries plain SKU first, then prefixed (S{siteId}_{sku}) for backward compatibility with products synced when prefix was used.
        /// Returns its ID if found, so we can update instead of create (avoids product_invalid_sku when product already exists).
        /// </summary>
        private static async Task<int?> FindProductIdBySkuAsync(string baseUrl, int siteId, string sku, HttpClient httpClient, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;
            var trimmed = sku.Trim();
            var wooSku = GetWooCommerceSku(siteId, trimmed);
            try
            {
                // Try plain SKU first (current sync uses no prefix)
                var url = $"{baseUrl}/products?sku={Uri.EscapeDataString(wooSku)}&per_page=1";
                var response = await httpClient.GetAsync(url, cancelToken);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancelToken);
                    var list = TryDeserialize<List<WooCommerceProductResponse>>(body);
                    var first = list?.FirstOrDefault();
                    if (first != null) return first.id;
                }
                // Fallback: try prefixed SKU for products synced when site prefix was used
                var prefixedSku = $"S{siteId}_{trimmed}";
                if (prefixedSku != wooSku)
                {
                    url = $"{baseUrl}/products?sku={Uri.EscapeDataString(prefixedSku)}&per_page=1";
                    response = await httpClient.GetAsync(url, cancelToken);
                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync(cancelToken);
                        var list = TryDeserialize<List<WooCommerceProductResponse>>(body);
                        var first = list?.FirstOrDefault();
                        return first?.id;
                    }
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Fetches existing product images from WooCommerce (id, src, name) so we can send id instead of src on update and avoid duplicating images in the media library.
        /// </summary>
        private static async Task<List<(int id, string? src, string? name)>?> GetWooCommerceProductImagesAsync(string baseUrl, int wooProductId, HttpClient httpClient, CancellationToken cancelToken)
        {
            try
            {
                var url = $"{baseUrl}/products/{wooProductId}";
                var response = await httpClient.GetAsync(url, cancelToken);
                if (!response.IsSuccessStatusCode) return null;
                var body = await response.Content.ReadAsStringAsync(cancelToken);
                var product = TryDeserialize<WooCommerceProductGetResponse>(body);
                if (product?.images == null) return null;
                return product.images.Select(img => (img.id, img.src, img.name)).ToList();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsPublicImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

            //var host = uri.Host.ToLowerInvariant();
            //if (host == "localhost" || host == "127.0.0.1" || host == "::1") return false;

            // ?????????: ????? ?? private ranges ??? ????
            return true;
        }

        // Helper classes for WooCommerce API responses
        private class WooCommerceCategoryResponse
        {
            public int id { get; set; }
        }

        private class WooCommerceAttributeResponse
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? slug { get; set; }
        }

        /// <summary>POST body for WooCommerce attribute term creation. "name" is required by the API.</summary>
        private class WooCommerceAttributeTermPayload
        {
            public string name { get; set; } = "";
        }

        private class WooCommerceProductResponse
        {
            public int id { get; set; }
        }

        /// <summary>GET product response - includes images with id and src to avoid duplicating on update.</summary>
        private class WooCommerceProductGetResponse
        {
            public List<WooCommerceImageItem>? images { get; set; }
        }

        private class WooCommerceImageItem
        {
            public int id { get; set; }
            public string? src { get; set; }
            public string? name { get; set; }
        }

        private class WooCommerceVariationResponse
        {
            public int id { get; set; }
        }

        /// <summary>For GET variations list: each variation has id and attributes (id, option).</summary>
        private class WooCommerceVariationListItem
        {
            public int id { get; set; }
            public List<WooCommerceVariationAttributeItem>? attributes { get; set; }
        }

        private class WooCommerceVariationAttributeItem
        {
            public int id { get; set; }
            public string? option { get; set; }
        }

        private static string NormalizeOptionKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();
            // collapse multiple spaces (including Hebrew/RTL spacing issues)
            s = Regex.Replace(s, @"\s+", " ");
            return s;
        }

        /// <summary>
        /// Resolve product weight for WooCommerce "weight" field (kg string).
        /// Uses Product.Weight first, then falls back to WeightConfig.UnitWeight for weighted products.
        /// </summary>
        private static string ResolveProductWeightForWoo(Product product, bool isWeighted)
        {
            if (!isWeighted)
                return "";

            if (product.Weight.HasValue && product.Weight.Value > 0)
                return product.Weight.Value.ToString(CultureInfo.InvariantCulture);

            var unitWeightRaw = product.WeightConfig?.UnitWeight;
            if (string.IsNullOrWhiteSpace(unitWeightRaw))
                return "";

            var normalized = unitWeightRaw.Trim().Replace(',', '.');
            if (!decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var unitWeightValue) || unitWeightValue <= 0)
                return "";

            var unitName = product.WeightConfig?.Unit?.Name?.Trim().ToLowerInvariant();
            var valueInKg = unitName is "g" or "gram" or "grams" or "גרם"
                ? unitWeightValue / 1000m
                : unitWeightValue;

            return valueInKg.ToString(CultureInfo.InvariantCulture);
        }
        public class WooErrorResponse
        {
            public string? code { get; set; }
            public string? message { get; set; }
            public WooErrorData? data { get; set; }

            public class WooErrorData
            {
                public int? status { get; set; }
                public int? resource_id { get; set; }
            }
        }
    }
}

