using AutoMapper;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;
using George.Common;
using George.Common.Utils;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Attribute = George.DB.Attribute;
using System.Globalization;
using ImageMagick;

namespace George.Services
{
    public partial class WooCommerceService : ServiceBase
    {
        /// <summary>HTTP client timeout for WooCommerce API calls (bulk sync can take many minutes).</summary>
        private static readonly TimeSpan WooCommerceHttpTimeout = TimeSpan.FromMinutes(30);

        /// <summary>Import processes products in batches; variation lists for each batch are prefetched in parallel.</summary>
        private const int WooImportProductBatchSize = 25;

        /// <summary>Max concurrent WooCommerce GET /products/{id}/variations calls while prefetching a batch.</summary>
        private const int WooImportVariationPrefetchParallelism = 8;

        /// <summary>Semaphore per (siteId, attributeName) so parallel product syncs don't create the same global attribute multiple times.</summary>
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> AttributeEnsureLocks = new ConcurrentDictionary<string, SemaphoreSlim>(StringComparer.Ordinal);

        /// <summary>Cached result of <c>GET {site}/wp-json/ed/v1/capabilities</c> → <c>product_labels</c>.</summary>
        private static readonly ConcurrentDictionary<string, bool> EdProductLabelsCapabilityCache = new(StringComparer.OrdinalIgnoreCase);

        private static readonly TimeSpan EdCapabilitiesProbeTimeout = TimeSpan.FromSeconds(15);

        private readonly SiteStorage _siteStorage;
        private readonly CategoryStorage _categoryStorage;
        private readonly ProductStorage _productStorage;
        private readonly AttributeStorage _attributeStorage;
        private readonly OrderStorage _orderStorage;
        private readonly BrandStorage _brandStorage;
        private readonly MediaStorage _mediaStorage;
        private readonly IFileStorage _fileStorage;
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
            BrandStorage brandStorage,
            MediaStorage mediaStorage,
            IFileStorage fileStorage,
            IHttpClientFactory httpClientFactory,
            IServiceScopeFactory scopeFactory
        ) : base(logger, mapper, cache)
        {
            _siteStorage = siteStorage;
            _categoryStorage = categoryStorage;
            _productStorage = productStorage;
            _attributeStorage = attributeStorage;
            _orderStorage = orderStorage;
            _brandStorage = brandStorage;
            _mediaStorage = mediaStorage;
            _fileStorage = fileStorage;
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
        /// Imports WooCommerce catalog into our side for a site and overwrites existing matched entities.
        /// </summary>
        public Task<IApiResponse<WooCommerceImportFromWooRes>> ImportFromWooCommerceAsync(
            WooCommerceSyncReq req,
            CancellationToken cancelToken) =>
            RunImportFromWooAsync(req, importProgress: null, cancelToken);

        /// <summary>Same import as <see cref="ImportFromWooCommerceAsync"/> with progress callbacks (e.g. NDJSON stream).</summary>
        public Task<IApiResponse<WooCommerceImportFromWooRes>> ImportFromWooCommerceWithProgressAsync(
            WooCommerceSyncReq req,
            IProgress<WooCommerceImportProgress>? importProgress,
            CancellationToken cancelToken) =>
            RunImportFromWooAsync(req, importProgress, cancelToken);

        private async Task<IApiResponse<WooCommerceImportFromWooRes>> RunImportFromWooAsync(
            WooCommerceSyncReq req,
            IProgress<WooCommerceImportProgress>? importProgress,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceImportFromWooRes> { Data = new WooCommerceImportFromWooRes() };
            try
            {
                var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken);
                if (site == null)
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                if (string.IsNullOrWhiteSpace(site.WooCommerceUrl) ||
                    string.IsNullOrWhiteSpace(site.WooCommerceKey) ||
                    string.IsNullOrWhiteSpace(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce integration not configured. Please set up your credentials in Store Settings.");
                }

                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = WooCommerceHttpTimeout;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                importProgress?.Report(new WooCommerceImportProgress { Phase = "fetch", Total = 2, Completed = 0 });
                var wooCategories = await FetchWooPagedAsync<WooImportCategoryItem>(httpClient, $"{baseUrl}/products/categories", cancelToken);
                importProgress?.Report(new WooCommerceImportProgress { Phase = "fetch", Total = 2, Completed = 1 });
                // Include draft/private/pending/future (status=any). Woo often keeps trashed posts out of "any" — merge a trash pass so counts match admin "All".
                var wooProductsRaw = await FetchWooPagedAsync<WooImportProductItem>(httpClient, $"{baseUrl}/products?status=any", cancelToken);
                try
                {
                    var trashedRows = await FetchWooPagedAsync<WooImportProductItem>(httpClient, $"{baseUrl}/products?status=trash", cancelToken);
                    if (trashedRows.Count > 0)
                    {
                        // Same post id must not appear twice in the merged feed (avoids false "duplicate" counts when any+trash overlap or plugins echo rows).
                        var idsAlready = wooProductsRaw.Where(w => w.id > 0).Select(w => w.id).ToHashSet();
                        var trashOnlyNew = trashedRows.Where(t => t.id > 0 && !idsAlready.Contains(t.id)).ToList();
                        var skippedTrashOverlap = trashedRows.Count - trashOnlyNew.Count;
                        if (skippedTrashOverlap > 0)
                        {
                            _logger.LogInformation(
                                "WooCommerce import: skipped {Skipped} trash product row(s) whose id already existed in the status=any feed (same Woo id in both lists).",
                                skippedTrashOverlap);
                        }
                        if (trashOnlyNew.Count > 0)
                        {
                            var before = wooProductsRaw.Count;
                            wooProductsRaw = wooProductsRaw.Concat(trashOnlyNew).ToList();
                            _logger.LogInformation(
                                "WooCommerce import: merged {TrashRows} trash-only product row(s) into feed ({Before} → {After} rows before REST id de-dupe).",
                                trashOnlyNew.Count,
                                before,
                                wooProductsRaw.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WooCommerce import: optional products?status=trash fetch failed; continuing with status=any only.");
                }

                var wooProducts = DedupeWooImportProductsByRestId(wooProductsRaw);
                var feedRowCountWithId = wooProductsRaw.Count(w => w.id > 0);
                var uniqueIdCount = wooProducts.Count(w => w.id > 0);
                response.Data.WooProductFeedRowCount = feedRowCountWithId;
                response.Data.WooProductUniqueIdCount = uniqueIdCount;
                response.Data.WooProductFeedDuplicates = BuildWooProductFeedDuplicateRows(wooProductsRaw);
                if (wooProducts.Count < wooProductsRaw.Count)
                {
                    _logger.LogWarning(
                        "WooCommerce import: product feed contained {Dup} duplicate REST id row(s) ({Raw} rows, {Unique} unique).",
                        wooProductsRaw.Count - wooProducts.Count,
                        wooProductsRaw.Count,
                        wooProducts.Count);
                }
                importProgress?.Report(new WooCommerceImportProgress { Phase = "fetch", Total = 2, Completed = 2 });

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<GeorgeDBContext>();
                using var tx = await db.Database.BeginTransactionAsync(cancelToken);

                // Must use Site tracked on this db context. Passing site from GetSiteAsync (other context / detached)
                // with Include(... Site) on categories/products causes: "another instance with the same key is already being tracked".
                var siteForImport = await db.Site.FirstOrDefaultAsync(s => s.Id == req.SiteId && !s.IsDeleted, cancelToken);
                if (siteForImport == null)
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");

                var importLookups = await LoadWooImportCatalogLookupsAsync(db, cancelToken);
                importProgress?.Report(new WooCommerceImportProgress { Phase = "categories", Total = wooCategories.Count, Completed = 0 });
                var categoryMap = await UpsertCategoriesFromWooAsync(db, siteForImport, wooCategories, response.Data, cancelToken);
                importProgress?.Report(new WooCommerceImportProgress { Phase = "categories", Total = wooCategories.Count, Completed = wooCategories.Count });

                // Brands: pulled in their own pass so each product can lookup local brand ids by Woo id.
                // Returns empty map (and no-ops) on stores running pre-9.6 WooCommerce; the legacy
                // _brand meta-key path keeps working as a fallback inside ApplyWooImportProductExtensionsAsync.
                importProgress?.Report(new WooCommerceImportProgress { Phase = "brands", Total = 1, Completed = 0 });
                var brandMap = await UpsertBrandsFromWooAsync(db, siteForImport, siteForImport.AccountId, httpClient, baseUrl, response.Data, cancelToken);
                importProgress?.Report(new WooCommerceImportProgress { Phase = "brands", Total = 1, Completed = 1 });

                await UpsertProductsFromWooAsync(
                    db,
                    siteForImport,
                    baseUrl,
                    httpClient,
                    wooProducts,
                    categoryMap,
                    brandMap,
                    importLookups,
                    response.Data,
                    importProgress,
                    cancelToken);

                await tx.CommitAsync(cancelToken);
                var dupRows = feedRowCountWithId - uniqueIdCount;
                response.Data.Message = dupRows > 0
                    ? $"Imported from WooCommerce: {wooCategories.Count} categories, {brandMap.Count} brands, {uniqueIdCount} unique Woo products ({feedRowCountWithId} API rows; {dupRows} duplicate id row(s) in the feed were merged into one product each)."
                    : $"Imported from WooCommerce: {wooCategories.Count} categories, {brandMap.Count} brands, and {uniqueIdCount} unique Woo products processed.";
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing from WooCommerce");
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
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
        /// Deletes a product from WooCommerce for the given site. Resolves Woo product id from <paramref name="wooCommerceId"/> when provided,
        /// otherwise by SKU (same rules as sync). When the product is linked to multiple sites, pass <c>wooCommerceId: null</c> so each store is matched by SKU only (avoids wrong id in another Woo store).
        /// </summary>
        public async Task<bool> DeleteProductFromWooCommerceForSiteAsync(int siteId, int? wooCommerceId, string? sku, CancellationToken cancelToken)
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

            int? wooId = wooCommerceId;
            if (!wooId.HasValue && !string.IsNullOrWhiteSpace(sku))
                wooId = await FindProductIdBySkuAsync(baseUrl, siteId, sku, httpClient, cancelToken);

            if (!wooId.HasValue)
            {
                _logger.LogDebug("Skipping WooCommerce product delete for site {SiteId}: no Woo id and no SKU match", siteId);
                return false;
            }

            var deleteUrl = $"{baseUrl}/products/{wooId.Value}?force=true";
            var deleteResponse = await httpClient.DeleteAsync(deleteUrl, cancelToken);
            if (deleteResponse.IsSuccessStatusCode)
            {
                _logger.LogInformation("Deleted WooCommerce product {WooId} for site {SiteId}", wooId.Value, siteId);
                return true;
            }

            var err = await deleteResponse.Content.ReadAsStringAsync(cancelToken);
            _logger.LogWarning("Failed to delete WooCommerce product {WooId} for site {SiteId}: {Status} {Error}", wooId.Value, siteId, deleteResponse.StatusCode, err);
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
        /// True when George stock is managed by numeric quantity (matches <see cref="ProductCatalogStockClassification.StockManagementTypeForApi"/> / import lookups, case-insensitive).
        /// Simple-product Woo payload used <c>Name == "quantity"</c> which broke when DB row was e.g. <c>Quantity</c> — WooCommerce then ignored <c>stock_quantity</c> until a product save rewrote the lookup row.
        /// </summary>
        private static bool IsStockQuantityManagementName(string? stockManagementTypeName)
        {
            if (string.IsNullOrWhiteSpace(stockManagementTypeName)) return false;
            var n = stockManagementTypeName.Trim();
            return string.Equals(n, "quantity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(n, "qty", StringComparison.OrdinalIgnoreCase);
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
                await EnsureAssignedBrandsSyncedToWooForSiteAsync(siteId, product, cancelToken).ConfigureAwait(false);

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
                // George "hidden" historically meant "unpublished / draft-like" for many flows; WooCommerce "private" is a different
                // visibility mode (often shown as "פרטי" in admin). Use "draft" so catalog imports and legacy data are not mis-tagged as private.
                if (product.Status?.Name == "hidden")
                    wooStatus = "draft";
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
                var wpJsonBaseForMedia = GetWordPressRestBaseUrlFromWooV3BaseUrl(baseUrl);
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
                            var lastSegment = new Uri(url, UriKind.Absolute).Segments.LastOrDefault()?.Trim('/');
                            if (!string.IsNullOrEmpty(lastSegment))
                                friendlyName = lastSegment;
                        }
                        catch { /* ignore URI parse */ }
                    }

                    // Same as media library "download to our storage": persist JPEG to our bucket/disk and point Media + ProductImage at the public URL, then WooCommerce sideloads that URL like any normal product image.
                    if (pi.MediaId.HasValue && await ImageRequiresJpegMediaUploadForWooAsync(url, friendlyName, cancelToken).ConfigureAwait(false))
                    {
                        var mirroredUrl = await TryMirrorProductImageToOurStorageForWooAsync(pi.MediaId.Value, url, cancelToken).ConfigureAwait(false);
                        if (!string.IsNullOrEmpty(mirroredUrl))
                        {
                            url = mirroredUrl;
                            _logger.LogInformation("Woo sync: mirrored product image to our storage for product {ProductId}, media {MediaId}", product.Id, pi.MediaId.Value);
                        }
                    }

                    // Modern formats (AVIF/HEIF/WebP) and negotiation CDNs (e.g. Wolt imageproxy) often fail WooCommerce URL sideload on WordPress; upload JPEG via wp/v2/media then attach by id.
                    if (await ImageRequiresJpegMediaUploadForWooAsync(url, friendlyName, cancelToken).ConfigureAwait(false))
                    {
                        var compatFile = GeorgeWooProductImageCompatFileName(product.Id, pi.MediaId, i);
                        if (existingWooImages != null)
                        {
                            var compatMatch = FindExistingWooImageByAttachmentName(existingWooImages, compatFile);
                            if (compatMatch.id != 0)
                            {
                                images.Add(new { id = compatMatch.id, position });
                                continue;
                            }
                        }

                        byte[] jpegBytes;
                        try
                        {
                            jpegBytes = await DownloadImageAndEncodeAsJpegAsync(url, cancelToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Woo sync: could not re-encode image to JPEG for product {ProductId}, url={Url}", product.Id, url);
                            if (TryAppendWoltJpegFormatQuery(url, out var woltAfterDecodeFail))
                            {
                                _logger.LogInformation("Woo sync: using Wolt JPEG URL sideload after decode failure product {ProductId}.", product.Id);
                                images.Add(new { src = woltAfterDecodeFail, name = compatFile, position });
                            }

                            continue;
                        }

                        var uploadResult = await TryUploadJpegToWordPressMediaLibraryAsync(httpClient, wpJsonBaseForMedia, jpegBytes, compatFile, cancelToken).ConfigureAwait(false);
                        if (uploadResult.MediaId.HasValue)
                            images.Add(new { id = uploadResult.MediaId.Value, position });
                        else if (TryAppendWoltJpegFormatQuery(url, out var woltJpegSideloadUrl))
                        {
                            _logger.LogInformation(
                                "Woo sync: wp/v2/media failed ({Status}) for product {ProductId}; using Wolt JPEG URL sideload (format=jpg).",
                                uploadResult.HttpStatus, product.Id);
                            images.Add(new { src = woltJpegSideloadUrl, name = compatFile, position });
                        }
                        else
                            _logger.LogWarning("Woo sync: wp/v2/media upload failed for product {ProductId}, file {File}; image skipped.", product.Id, compatFile);
                        continue;
                    }

                    // WordPress sideload derives the upload file type from the filename; extensionless URLs (e.g. CDN asset IDs) fail with "not allowed to upload this file type".
                    var sideloadFileName = await ResolveWooImageSideloadFileNameAsync(url, friendlyName, cancelToken).ConfigureAwait(false);
                    sideloadFileName = GeorgeWooProductImageSideloadFileName(product.Id, pi.MediaId, sideloadFileName);

                    if (existingWooImages != null)
                    {
                        // Prefer exact attachment name (includes Media id when known) so replacing an image uploads new bytes instead of reusing an old Woo attachment matched only by display name / slot index.
                        var match = FindExistingWooImageByAttachmentName(existingWooImages, sideloadFileName);
                        if (match.id == 0 && !pi.MediaId.HasValue)
                        {
                            match = existingWooImages.FirstOrDefault(ex => WooProductImageAttachmentNameMatchesHint(ex.name, friendlyName, sideloadFileName));
                            if (match.id == 0)
                                match = existingWooImages.FirstOrDefault(ex => string.Equals((ex.src ?? "").Trim(), url, StringComparison.OrdinalIgnoreCase));
                        }
                        if (match.id != 0)
                            images.Add(new { id = match.id, position });
                        else
                            images.Add(new { src = url, name = sideloadFileName, position });
                    }
                    else
                        images.Add(new { src = url, name = sideloadFileName, position });
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
                // ACF true/false field: value goes in "show_as_ml" (1/0), reference key goes in "_show_as_ml"
                metaData.Add(new { key = "show_as_ml", value = showAsMl ? "1" : "0" });
                metaData.Add(new { key = "_show_as_ml", value = "field_699f090751cad" });
                AppendWooAcfStoreLabelMeta(metaData, product);
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
                        var ocwsuWeightUnits = MapOcwsuProductWeightUnits(weightConfig.Unit?.Name);
                        metaData.Add(new { key = "_ocwsu_product_weight_units", value = ocwsuWeightUnits });
                        metaData.Add(new { key = "ocwsu_product_weight_units_", value = ocwsuWeightUnits });
                        metaData.Add(new { key = "_ocwsu_display_price_per_100g", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        metaData.Add(new { key = "ocwsu_display_price_per_100g_", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        if (soldByUnits == "yes")
                        {
                            var showUnitPrice = weightConfig.ShowUnitPrice == true;
                            metaData.Add(new { key = "_ocwsu_display_price_per_fixed_unit", value = showUnitPrice ? "yes" : "no" });
                            metaData.Add(new { key = "ocwsu_display_price_per_fixed_unit_", value = showUnitPrice ? "yes" : "no" });
                            var soldByLabelValue = showUnitPrice
                                ? OcwsuSoldByLabel.ToApiValue(weightConfig.SoldByLabel ?? OcwsuSoldByLabel.DefaultKey)
                                : "";
                            metaData.Add(new { key = "_ocwsu_display_price_per_fixed_unit_label", value = soldByLabelValue });
                            metaData.Add(new { key = "ocwsu_display_price_per_fixed_unit_label_", value = soldByLabelValue });
                        }
                        metaData.Add(new { key = "_ocwsu_min_weight", value = weightConfig.StartWeight ?? "" });
                        metaData.Add(new { key = "ocwsu_min_weight_", value = weightConfig.StartWeight ?? "" });
                        metaData.Add(new { key = "_ocwsu_weight_step", value = weightConfig.Step ?? "" });
                        metaData.Add(new { key = "ocwsu_weight_step_", value = weightConfig.Step ?? "" });
                        // WooCommerce plugin expects "fixed" for משקל קבוע and "variable" for משקל משתנה.
                        // Our modes: "average" = fixed weight per unit, "variable" = variable weight, "by_variant" = weight from variant.
                        // "by_variant" maps to "variable" in WooCommerce (_ocwsu_unit_weight_type); the "get from variation" flag is sent separately.
                        // When UnitWeightMode is null, fall back to FixedWeightPerUnit (false = variable, true = fixed).
                        string unitWeightTypeForWoo;
                        var unitWeightModeName = weightConfig.UnitWeightMode?.Name;
                        if (string.Equals(unitWeightModeName, "average", StringComparison.OrdinalIgnoreCase))
                            unitWeightTypeForWoo = "fixed";
                        else if (string.Equals(unitWeightModeName, "variable", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(unitWeightModeName, "by_variant", StringComparison.OrdinalIgnoreCase))
                            unitWeightTypeForWoo = "variable";
                        else if (weightConfig.FixedWeightPerUnit == true)
                            unitWeightTypeForWoo = "fixed";
                        else if (weightConfig.FixedWeightPerUnit == false)
                            unitWeightTypeForWoo = "variable";
                        else
                            unitWeightTypeForWoo = "";
                        metaData.Add(new { key = "_ocwsu_unit_weight_type", value = unitWeightTypeForWoo });
                        metaData.Add(new { key = "ocwsu_unit_weight_type_", value = unitWeightTypeForWoo });
                        metaData.Add(new { key = "_ocwsu_unit_weight", value = weightConfig.UnitWeight ?? "" });
                        metaData.Add(new { key = "ocwsu_unit_weight_", value = weightConfig.UnitWeight ?? "" });
                        // WooCommerce expects one weight option per line; we store comma-separated.
                        // Inactive weights may follow "##" (admin quick stock); never send those to Woo.
                        var weightOptionsRaw = weightConfig.WeightOptions ?? "";
                        var weightOptionsActiveOnly = weightOptionsRaw.Contains("##", StringComparison.Ordinal)
                            ? weightOptionsRaw.Split("##", 2, StringSplitOptions.TrimEntries)[0]
                            : weightOptionsRaw;
                        var unitWeightOptionsForWoo = string.IsNullOrWhiteSpace(weightOptionsActiveOnly)
                            ? ""
                            : string.Join("\n", weightOptionsActiveOnly.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0));
                        metaData.Add(new { key = "_ocwsu_unit_weight_options", value = unitWeightOptionsForWoo });
                        metaData.Add(new { key = "ocwsu_unit_weight_options_", value = unitWeightOptionsForWoo });
                        var getWeightFromVariation = weightConfig.WeightByVariant == true ? "yes" : "no";
                        metaData.Add(new { key = "_ocwsu_get_weight_from_variation", value = getWeightFromVariation });
                        metaData.Add(new { key = "ocwsu_get_weight_from_variation_", value = getWeightFromVariation });
                    }
                }

                var wooSku = GetWooCommerceSku(siteId, product.Sku);
                // WooCommerce core "weight" (משלוח / product data) is for non-weighable catalog weight only.
                // Weighable products use OCWSU meta (_ocwsu_*); if we keep sending Product.Weight here, an old value (e.g. 0.3 kg from before switching to שקיל) never clears in Woo.
                var productWeightForWoo = isWeighted
                    ? ""
                    : (product.Weight.HasValue && product.Weight.Value > 0
                        ? product.Weight.Value.ToString(CultureInfo.InvariantCulture)
                        : "");
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

                if (!string.IsNullOrWhiteSpace(product.Slug))
                    wooProduct["slug"] = product.Slug.Trim();

                // Linked products (WooCommerce admin: מוצרים משודרגים / מוצרים משלימים) — REST keys are upsell_ids / cross_sell_ids.
                // Local RelatedProduct = up-sells; ComplementaryProduct = cross-sells.
                var upsellIds = await ResolveWooCommerceIdsForLinkedProductsAsync(baseUrl, siteId, product.RelatedProduct, httpClient, cancelToken).ConfigureAwait(false);
                var crossSellIds = await ResolveWooCommerceIdsForLinkedProductsAsync(baseUrl, siteId, product.ComplementaryProduct, httpClient, cancelToken).ConfigureAwait(false);
                wooProduct["upsell_ids"] = upsellIds;
                wooProduct["cross_sell_ids"] = crossSellIds;

                // Brand assignment — Woo REST expects an array of objects with "id" (see Products API brands write-mode).
                // Uses DB state after EnsureAssignedBrandsSyncedToWooForSiteAsync so new brands get IDs first.
                // Only include the "brands" key when at least one brand is synced; otherwise leave the
                // existing Woo-side assignment alone (avoids accidentally clearing brands when our local
                // brands haven't been pushed yet).
                var brandIds = await ResolveWooBrandIdsForProductOnSiteAsync(siteId, product, cancelToken).ConfigureAwait(false);
                if (brandIds.Count > 0)
                    wooProduct["brands"] = brandIds.Select(id => (object)new Dictionary<string, object> { ["id"] = id }).ToList();

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
                    wooProduct["manage_stock"] = IsStockQuantityManagementName(product.StockManagementType?.Name);
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
                            var attributeValues = GetProductOptionValuesForWooSync(option, product);

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
                                ["options"] = GetProductOptionValuesForWooSync(option, product)
                            };
                            if (!string.IsNullOrEmpty(slug))
                                dict["name"] = slug;
                            return dict;
                        })
                        .Cast<object>()
                        .ToList() ?? new List<object>();

                    wooProduct["attributes"] = attributes;

                    // Variable product: parent-level inventory when stock is managed at product level (not per variation).
                    // WooCommerce: "Settings below apply to all variations without manual stock management enabled."
                    // Without this, _manage_stock stays unchecked and _stock wrong in WP admin despite app showing quantity.
                    var smt = product.StockManagementType?.Name;
                    if (IsStockQuantityManagementName(smt))
                    {
                        wooProduct["manage_stock"] = true;
                        wooProduct["stock_quantity"] = product.StockQuantity ?? 0;
                        wooProduct["stock_status"] = stockStatus;
                        wooProduct["backorders"] = product.StockStatus?.Name == "on_backorder" ? "yes" : "no";
                    }
                    else if (string.Equals(smt, "status", StringComparison.OrdinalIgnoreCase))
                    {
                        wooProduct["manage_stock"] = false;
                        wooProduct["stock_status"] = stockStatus;
                        wooProduct["backorders"] = product.StockStatus?.Name == "on_backorder" ? "yes" : "no";
                    }
                    else
                    {
                        // variation-level stock: parent does not track quantity; SyncProductVariantsAsync sets each variation.
                        wooProduct["manage_stock"] = false;
                        wooProduct["stock_status"] = stockStatus;
                        wooProduct["backorders"] = product.StockStatus?.Name == "on_backorder" ? "yes" : "no";
                    }
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

                // Store label ACF flags via custom REST namespace ed/v1 (no WooCommerce Basic auth per site plugin).
                if (wooCommerceId.HasValue)
                {
                    try
                    {
                        await SyncProductEdAcfStoreLabelsAsync(baseUrl, wooCommerceId.Value, product, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ED/v1 ACF label sync failed for product {ProductId} Woo id {WooId}; main WooCommerce product sync succeeded.", product.Id, wooCommerceId.Value);
                    }

                    try
                    {
                        await SyncProductOcwsuFixedUnitPriceDisplayAsync(baseUrl, wooCommerceId.Value, product, cancelToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "ED/v1 OCWSU fixed unit price display sync failed for product {ProductId} Woo id {WooId}; main WooCommerce product sync succeeded.", product.Id, wooCommerceId.Value);
                    }
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
        /// POSTs boolean ACF-backed flags to <c>{site}/wp-json/ed/v1/...</c> (Omer's endpoints). Uses a separate HTTP client without WooCommerce REST credentials.
        /// Skipped when <c>GET {site}/wp-json/ed/v1/capabilities</c> reports <c>product_labels: false</c> or the endpoint is unavailable.
        /// </summary>
        private async Task SyncProductEdAcfStoreLabelsAsync(string wcV3BaseUrl, int wooProductId, Product product, CancellationToken cancelToken)
        {
            var idx = wcV3BaseUrl.IndexOf("/wp-json", StringComparison.OrdinalIgnoreCase);
            var siteRoot = idx > 0 ? wcV3BaseUrl.Substring(0, idx).TrimEnd('/') : wcV3BaseUrl.TrimEnd('/');

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = EdCapabilitiesProbeTimeout;
            http.DefaultRequestHeaders.Clear();

            if (!await SiteSupportsEdProductLabelsAsync(siteRoot, http, cancelToken).ConfigureAwait(false))
                return;

            http.Timeout = TimeSpan.FromSeconds(60);

            var now = DateTime.UtcNow;
            var passoverEffective = product.LabelKosherForPassover &&
                                    (!product.LabelKosherForPassoverEndDate.HasValue || product.LabelKosherForPassoverEndDate.Value > now);
            var newEffective = product.LabelNew &&
                               (!product.LabelNewEndDate.HasValue || product.LabelNewEndDate.Value > now);

            async Task PostBool(string path, string fieldKey, bool value)
            {
                var url = $"{siteRoot}/wp-json/ed/v1/{path}";
                var json = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["product_id"] = wooProductId,
                    [fieldKey] = value
                });
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await http.PostAsync(url, content, cancelToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                    _logger.LogWarning("ED/v1 POST {Path} failed for Woo product {WooId}: {Status} {Body}", path, wooProductId, (int)resp.StatusCode, err);
                }
            }

            await PostBool("product-frozen", "frozen", product.LabelFrozen).ConfigureAwait(false);
            await PostBool("product-gluten-free", "gluten_free", product.LabelGlutenFree).ConfigureAwait(false);
            await PostBool("product-not-kosher", "not_kosher", product.LabelNotKosher).ConfigureAwait(false);
            await PostBool("product-kosher-for-passover", "kosher_for_passover", passoverEffective).ConfigureAwait(false);
            await PostBool("product-bestseller", "bestseller", product.LabelBestseller).ConfigureAwait(false);
            await PostBool("product-low-availability", "low_availability", product.LabelLowAvailability).ConfigureAwait(false);
            await PostBool("product-readytocook", "readytocook", product.LabelReadyToCook).ConfigureAwait(false);
            await PostBool("product-natural", "natural", product.LabelNatural).ConfigureAwait(false);
            await PostBool("product-sugarfree", "sugarfree", product.LabelSugarFree).ConfigureAwait(false);
            await PostBool("product-lactosefree", "lactosefree", product.LabelLactoseFree).ConfigureAwait(false);
            await PostBool("product-new", "new", newEffective).ConfigureAwait(false);
        }

        /// <summary>
        /// POSTs <c>display_price_per_fixed_unit</c> and label to
        /// <c>{site}/wp-json/ed/v1/product-ocwsu-fixed-unit-price-display</c>.
        /// </summary>
        private async Task SyncProductOcwsuFixedUnitPriceDisplayAsync(string wcV3BaseUrl, int wooProductId, Product product, CancellationToken cancelToken)
        {
            var setupTypeName = product.SetupType?.Name ?? "";
            var isWeightedBySetup = setupTypeName is "by_weight" or "by_unit" or "by_unit_and_weight";
            var isWeighted = product.IsWeighted == true || (product.IsWeighted != false && isWeightedBySetup);
            if (!isWeighted || product.WeightConfig == null)
                return;

            var soldByUnits = setupTypeName is "by_unit" or "by_unit_and_weight";
            if (!soldByUnits)
                return;

            var wc = product.WeightConfig;
            var showUnitPrice = wc.ShowUnitPrice == true;

            var idx = wcV3BaseUrl.IndexOf("/wp-json", StringComparison.OrdinalIgnoreCase);
            var siteRoot = idx > 0 ? wcV3BaseUrl.Substring(0, idx).TrimEnd('/') : wcV3BaseUrl.TrimEnd('/');

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(60);
            http.DefaultRequestHeaders.Clear();

            var url = $"{siteRoot}/wp-json/ed/v1/product-ocwsu-fixed-unit-price-display";
            var payload = new Dictionary<string, object>
            {
                ["product_id"] = wooProductId,
                ["display_price_per_fixed_unit"] = showUnitPrice
            };
            if (showUnitPrice)
            {
                var labelKey = wc.SoldByLabel ?? OcwsuSoldByLabel.DefaultKey;
                payload["display_price_per_fixed_unit_label"] = OcwsuSoldByLabel.ToApiValue(labelKey);
            }
            else
            {
                payload["display_price_per_fixed_unit_label"] = "";
            }
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(url, content, cancelToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                _logger.LogWarning(
                    "ED/v1 POST product-ocwsu-fixed-unit-price-display failed for Woo product {WooId}: {Status} {Body}",
                    wooProductId, (int)resp.StatusCode, err);
            }
        }

        /// <summary>Manual sync of OCWSU fixed-unit price display for one catalog product on a site.</summary>
        public async Task<IApiResponse<OcwsuFixedUnitPriceDisplayRes>> SyncOcwsuFixedUnitPriceDisplayAsync(
            SyncOcwsuFixedUnitPriceDisplayReq request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<OcwsuFixedUnitPriceDisplayRes>();

            var site = await _siteStorage.GetSiteAsync(request.SiteId, cancelToken).ConfigureAwait(false);
            if (site == null || string.IsNullOrWhiteSpace(site.WooCommerceUrl))
                return CreateResponse(response, StatusCode.ItemNotFound, "Site not found or WooCommerce URL is not configured.");

            var product = await _productStorage.GetProductAsync(request.ProductId, cancelToken).ConfigureAwait(false);
            if (product == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Product not found.");

            var wooProductId = product.WooCommerceId;
            if (!wooProductId.HasValue || wooProductId.Value <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "Product is not linked to a WooCommerce product id.");

            var displayUnitPrice = request.DisplayPricePerFixedUnit
                ?? product.WeightConfig?.ShowUnitPrice == true;
            var labelKey = request.DisplayPricePerFixedUnitLabel
                ?? product.WeightConfig?.SoldByLabel
                ?? OcwsuSoldByLabel.DefaultKey;

            var siteRoot = site.WooCommerceUrl.TrimEnd('/');
            var idx = siteRoot.IndexOf("/wp-json", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
                siteRoot = siteRoot.Substring(0, idx).TrimEnd('/');

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = TimeSpan.FromSeconds(60);

            var url = $"{siteRoot}/wp-json/ed/v1/product-ocwsu-fixed-unit-price-display";
            var payload = new Dictionary<string, object>
            {
                ["product_id"] = wooProductId.Value,
                ["display_price_per_fixed_unit"] = displayUnitPrice
            };
            if (displayUnitPrice)
                payload["display_price_per_fixed_unit_label"] = OcwsuSoldByLabel.ToApiValue(labelKey);
            else
                payload["display_price_per_fixed_unit_label"] = "";
            var json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(url, content, cancelToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return CreateResponse(response, StatusCode.InvalidRequest, $"WooCommerce OCWSU sync failed ({(int)resp.StatusCode}): {body}");

            OcwsuFixedUnitPriceDisplayRes? parsed = null;
            try
            {
                parsed = JsonSerializer.Deserialize<OcwsuFixedUnitPriceDisplayRes>(body, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse OCWSU fixed unit price display response for product {ProductId}", request.ProductId);
            }

            response.Data = parsed ?? new OcwsuFixedUnitPriceDisplayRes
            {
                ProductId = wooProductId.Value,
                DisplayPricePerFixedUnit = displayUnitPrice,
                DisplayPricePerFixedUnitLabel = labelKey,
                Success = true
            };
            response.Data.ProductId = wooProductId.Value;
            response.Data.Success = true;
            return response;
        }

        /// <summary>
        /// Returns whether the WooCommerce site exposes product label sync via <c>GET /wp-json/ed/v1/capabilities</c> (<c>product_labels: true</c>).
        /// Result is cached per site root for the lifetime of the process.
        /// </summary>
        private async Task<bool> SiteSupportsEdProductLabelsAsync(string siteRoot, HttpClient http, CancellationToken cancelToken)
        {
            if (EdProductLabelsCapabilityCache.TryGetValue(siteRoot, out var cached))
                return cached;

            var supported = await ProbeEdProductLabelsCapabilityAsync(siteRoot, http, cancelToken).ConfigureAwait(false);
            EdProductLabelsCapabilityCache[siteRoot] = supported;
            return supported;
        }

        private async Task<bool> ProbeEdProductLabelsCapabilityAsync(string siteRoot, HttpClient http, CancellationToken cancelToken)
        {
            var url = $"{siteRoot}/wp-json/ed/v1/capabilities";
            try
            {
                using var resp = await http.GetAsync(url, cancelToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "ED/v1 product label sync disabled for {SiteRoot}: capabilities probe returned HTTP {Status}.",
                        siteRoot, (int)resp.StatusCode);
                    return false;
                }

                var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("product_labels", out var productLabelsEl) &&
                    (productLabelsEl.ValueKind == JsonValueKind.True || productLabelsEl.ValueKind == JsonValueKind.False))
                {
                    var supported = productLabelsEl.GetBoolean();
                    if (!supported)
                    {
                        _logger.LogInformation(
                            "ED/v1 product label sync disabled for {SiteRoot}: capabilities.product_labels is false.",
                            siteRoot);
                    }
                    return supported;
                }

                _logger.LogInformation(
                    "ED/v1 product label sync disabled for {SiteRoot}: capabilities response missing product_labels.",
                    siteRoot);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex,
                    "ED/v1 product label sync disabled for {SiteRoot}: capabilities probe failed.",
                    siteRoot);
                return false;
            }
        }

        /// <summary>
        /// Updates the order status on the store side (oc-storeos under <see cref="Site.WooCommerceUrl"/>, or WooCommerce REST). When that site URL is set, POSTs full order to <c>.../wp-json/oc-storeos/v1/orders</c>. Otherwise PUTs status to wc/v3 using <see cref="Site.WooCommerceUrl"/>. Does not throw; logs errors.
        /// </summary>
        public async Task UpdateOrderStatusAsync(int siteId, string wooOrderId, string status, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(wooOrderId)) return;
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null || site.WooCommerceEnabled != true) return;
            var ocV1Base = OcStoreosApiUrls.V1BaseFromWooCommerceRoot(site.WooCommerceUrl);
            var useOcStoreosOrderApi = !string.IsNullOrEmpty(ocV1Base);
            if (useOcStoreosOrderApi)
            {
                var wooId = wooOrderId.Trim();
                var order = await _orderStorage.GetOrderBySiteAndExternalIdAsync(siteId, wooId, cancelToken);
                if (order != null)
                {
                    _logger.LogInformation(
                        "WooCommerce UpdateOrderStatus: oc-storeos full POST sync. siteId={SiteId}, externalStoreOrderId={ExternalStoreOrderId}, internalOrderId={InternalOrderId}, requestedStatusHint={RequestedStatus}",
                        siteId, wooId, order.Id, status);
                    await SyncOrderToOcStoreosAsync(siteId, order.Id, cancelToken);
                }
                else
                {
                    _logger.LogWarning(
                        "WooCommerce UpdateOrderStatus: oc-storeos sync skipped — no local order for external id. siteId={SiteId}, externalStoreOrderId={ExternalStoreOrderId}, requestedStatusHint={RequestedStatus}",
                        siteId, wooId, status);
                }
                return;
            }
            if (string.IsNullOrEmpty(site.WooCommerceKey) || string.IsNullOrEmpty(site.WooCommerceSecret)) return;
            if (string.IsNullOrEmpty(site.WooCommerceUrl)) return;
            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
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
        /// Syncs full order to oc-storeos API: POST <c>{WooCommerceUrl}/wp-json/oc-storeos/v1/orders</c>. Body matches ingest shape: orderNumber, status, customer, shippingAddress, shippingInfo, items (sku + quantity), shippingTotal, customerNotes. Quantities prefer picked values when set.
        /// </summary>
        public async Task SyncOrderToOcStoreosAsync(int siteId, int orderId, CancellationToken cancelToken)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            var ocV1Base = OcStoreosApiUrls.V1BaseFromWooCommerceRoot(site?.WooCommerceUrl);
            if (site == null || string.IsNullOrWhiteSpace(ocV1Base))
            {
                _logger.LogWarning(
                    "oc-storeos sync skipped: site missing or WooCommerceUrl empty. siteId={SiteId}, internalOrderId={InternalOrderId}",
                    siteId, orderId);
                return;
            }
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken);
            if (order == null || string.IsNullOrWhiteSpace(order.ExternalOrderId))
            {
                _logger.LogWarning(
                    "oc-storeos sync skipped: order not found or ExternalOrderId empty. siteId={SiteId}, internalOrderId={InternalOrderId}",
                    siteId, orderId);
                return;
            }
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
            var items = new List<Dictionary<string, object?>>();
            foreach (var line in order.OrderItem?.OrderBy(i => i.SortOrder) ?? Enumerable.Empty<OrderItem>())
            {
                var qtyBase = line.PickedQuantity is > 0 ? line.PickedQuantity.Value : line.Quantity;
                var qty = qtyBase > 0 ? qtyBase : 1;
                var row = new Dictionary<string, object?>();
                if (line.ProductId.HasValue && productSkuAndWoo.TryGetValue(line.ProductId.Value, out var skuWoo))
                {
                    if (!string.IsNullOrWhiteSpace(skuWoo.Sku))
                    {
                        row["sku"] = skuWoo.Sku.Trim();
                        row["quantity"] = qty;
                    }
                    else if (skuWoo.WooCommerceId.HasValue)
                    {
                        row["productId"] = skuWoo.WooCommerceId.Value;
                        row["quantity"] = qty;
                    }
                    else
                        row["quantity"] = qty;
                }
                else
                    row["quantity"] = qty;
                row["name"] = line.Title ?? "";
                row["variationId"] = line.WooCommerceVariationId;
                row["variants"] = BuildWooVariantsFromVariantTitle(line.VariantTitle);
                row["note"] = line.Notes;
                row["productNote"] = line.Notes;
                row["unitPrice"] = line.PricePerUnit;
                row["lineTotal"] = line.TotalPrice;
                row["saleUnits"] = line.SaleUnits;
                row["saleTotalWeight"] = line.SaleTotalWeight;
                if (line.WooCommerceProductId.HasValue)
                    row["productId"] = line.WooCommerceProductId.Value;
                items.Add(row);
            }
            var deliveryDate = order.DeliveryDate ?? order.PickupDate;
            var deliveryTime = order.DeliveryTime ?? order.PickupTime;
            ParseDeliverySlotWindow(deliveryTime, out var slotStart, out var slotEnd);
            var deliveryType = (order.DeliveryType ?? "").Trim();
            var shippingType = string.Equals(deliveryType, "Pickup", StringComparison.OrdinalIgnoreCase) ? "pickup" : "delivery";
            object orderNumberValue = int.TryParse(order.ExternalOrderId.Trim(), out var wooOid) ? wooOid : order.ExternalOrderId.Trim();
            var shippingStreet = !string.IsNullOrWhiteSpace(order.DeliveryStreet)
                ? order.DeliveryStreet.Trim()
                : order.DeliveryAddress?.Trim();
            var shippingCity = string.IsNullOrWhiteSpace(order.DeliveryCity) ? "" : order.DeliveryCity.Trim();
            var shippingZip = TryExtractPostalCodeFromOrderAddress(order);
            var safeOrderDate = order.CreationTime == default ? DateTime.UtcNow : order.CreationTime;
            var payload = new Dictionary<string, object?>
            {
                ["orderNumber"] = orderNumberValue,
                ["externalOrderId"] = order.ExternalOrderId,
                ["source"] = order.Source,
                ["siteId"] = order.WooCommerceSiteId ?? siteId.ToString(CultureInfo.InvariantCulture),
                ["status"] = wcStatus ?? "pending",
                ["orderDate"] = safeOrderDate.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["customer"] = new Dictionary<string, string?>
                {
                    ["name"] = order.CustomerName ?? "",
                    ["email"] = order.CustomerEmail ?? "",
                    ["phone"] = order.CustomerPhone ?? ""
                },
                ["shippingAddress"] = new Dictionary<string, string?>
                {
                    ["street"] = shippingStreet ?? "",
                    ["city"] = shippingCity,
                    ["zip"] = shippingZip
                },
                ["shippingInfo"] = new Dictionary<string, object?>
                {
                    ["type"] = shippingType,
                    ["date"] = deliveryDate.HasValue ? deliveryDate.Value.ToString("yyyy-MM-dd") : null,
                    ["slotStart"] = slotStart,
                    ["slotEnd"] = slotEnd
                },
                ["items"] = items,
                ["shippingTotal"] = order.ShippingCost ?? 0,
                ["orderTotal"] = order.Total,
                ["customerNotes"] = order.CustomerNote ?? "",
                ["billing_notes"] = order.BillingNotes,
                ["internalOrderNotes"] = order.InternalOrderNotes,
                ["paymentMethod"] = order.PaymentMethod,
                ["paymentMethodTitle"] = order.PaymentMethodTitle,
                ["shipping_label"] = order.ShippingLabel,
                ["payment_label"] = order.PaymentLabel
            };
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var body = JsonSerializer.Serialize(payload, jsonOptions);
            _logger.LogInformation(
                "oc-storeos POST /orders starting. siteId={SiteId}, internalOrderId={InternalOrderId}, storeOrderId={StoreOrderId}, mappedStatus={MappedStatus}, itemCount={ItemCount}, auth=None, bodyChars={BodyChars}",
                siteId, orderId, order.ExternalOrderId, wcStatus ?? "pending", items.Count, body.Length);
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);
            httpClient.DefaultRequestHeaders.Clear();
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            var url = $"{ocV1Base}/orders";
            var response = await httpClient.PostAsync(url, content, cancelToken);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "oc-storeos POST /orders succeeded. siteId={SiteId}, internalOrderId={InternalOrderId}, storeOrderId={StoreOrderId}, httpStatus={HttpStatus}",
                    siteId, orderId, order.ExternalOrderId, (int)response.StatusCode);
            }
            else
            {
                var err = await response.Content.ReadAsStringAsync(cancelToken);
                _logger.LogWarning(
                    "oc-storeos POST /orders failed. siteId={SiteId}, internalOrderId={InternalOrderId}, storeOrderId={StoreOrderId}, httpStatus={HttpStatus}, error={Error}",
                    siteId, orderId, order.ExternalOrderId, (int)response.StatusCode, err);
            }
        }

        /// <summary>Parses slot text like "11:00 - 12:00" or "11:00-12:00" into start/end; single time yields both equal.</summary>
        private static void ParseDeliverySlotWindow(string? deliveryOrPickupTime, out string? slotStart, out string? slotEnd)
        {
            slotStart = null;
            slotEnd = null;
            if (string.IsNullOrWhiteSpace(deliveryOrPickupTime))
                return;
            var t = deliveryOrPickupTime.Trim();
            foreach (var sep in new[] { " - ", " – ", " — ", "-", "–", "—" })
            {
                var idx = t.IndexOf(sep, StringComparison.Ordinal);
                if (idx <= 0) continue;
                var a = t[..idx].Trim();
                var b = t[(idx + sep.Length)..].Trim();
                if (!string.IsNullOrEmpty(a))
                    slotStart = a;
                slotEnd = string.IsNullOrEmpty(b) ? slotStart : b;
                return;
            }
            slotStart = t;
            slotEnd = t;
        }

        /// <summary>Best-effort postal code from combined address lines (no dedicated zip column).</summary>
        private static string TryExtractPostalCodeFromOrderAddress(Order order)
        {
            var blobs = new[] { order.DeliveryAddress, order.DeliveryStreet, order.DeliveryCity };
            foreach (var blob in blobs)
            {
                if (string.IsNullOrWhiteSpace(blob)) continue;
                var m = Regex.Match(blob, @"\b(\d{5,7})\b");
                if (m.Success) return m.Groups[1].Value;
            }
            return "";
        }

        /// <summary>Converts stored variant title text into Woo-like variants array [{id,name}] (id unknown => null).</summary>
        private static List<Dictionary<string, object?>>? BuildWooVariantsFromVariantTitle(string? variantTitle)
        {
            if (string.IsNullOrWhiteSpace(variantTitle))
                return null;
            var parts = variantTitle
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (parts.Count == 0)
                return null;
            return parts
                .Select(p => new Dictionary<string, object?> { ["id"] = null, ["name"] = p })
                .ToList();
        }

        /// <summary>Statuses aligned with store ingest (e.g. pending for new).</summary>
        private static string MapOurStatusToWooCommerceForOcStoreos(string? ourStatus)
        {
            if (string.IsNullOrWhiteSpace(ourStatus)) return "pending";
            var s = ourStatus.Trim();
            if (string.Equals(s, "New", StringComparison.OrdinalIgnoreCase)) return "pending";
            if (string.Equals(s, "InTreatment", StringComparison.OrdinalIgnoreCase)) return "processing";
            if (string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Completed", StringComparison.OrdinalIgnoreCase)) return "completed";
            if (string.Equals(s, "Cancelled", StringComparison.OrdinalIgnoreCase)) return "cancelled";
            return "pending";
        }

        /// <summary>
        /// Syncs only menu_order to WooCommerce for the given ordered product IDs (e.g. after reorder).
        /// When <paramref name="onlyProductIds"/> is set, PUTs are sent only for those products (typically ones whose order changed).
        /// </summary>
        public async Task SyncMenuOrderOnlyAsync(
            int siteId,
            List<int> orderedProductIds,
            IReadOnlySet<int>? onlyProductIds,
            CancellationToken cancelToken)
        {
            if (orderedProductIds == null || !orderedProductIds.Any()) return;
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null || string.IsNullOrEmpty(site.WooCommerceUrl) || string.IsNullOrEmpty(site.WooCommerceKey) || string.IsNullOrEmpty(site.WooCommerceSecret))
                return;
            var orders = await _productStorage.GetWooCommerceIdAndDisplayOrderForSiteAsync(orderedProductIds, siteId, cancelToken, onlyProductIds);
            if (orders.Count == 0) return;
            var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
            var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromMinutes(2);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");
            const int concurrency = 3;
            for (var i = 0; i < orders.Count; i += concurrency)
            {
                var batch = orders.Skip(i).Take(concurrency).ToList();
                var tasks = batch.Select(o => UpdateWooCommerceProductMenuOrderAsync(baseUrl, o.WooCommerceId, o.DisplayOrder, httpClient, cancelToken));
                await Task.WhenAll(tasks);
                if (i + concurrency < orders.Count)
                    await Task.Delay(150, cancelToken);
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

        /// <summary>WooCommerce REST requires integer stock_quantity on variations; George may store kg decimals for weighable products.</summary>
        private static int ToWooVariationStockQuantity(decimal? stockQuantity, bool trackQuantity)
        {
            if (!trackQuantity)
                return (stockQuantity ?? 0) > 0 ? 1 : 0;
            return (int)Math.Round(stockQuantity ?? 0, MidpointRounding.AwayFromZero);
        }

        private static string FormatWooPrice(decimal? price) =>
            price.HasValue ? price.Value.ToString("F2", CultureInfo.InvariantCulture) : "0";

        private static readonly JsonSerializerOptions WooVariationJsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

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
            // Weighable products normally omit WC native weight (OCWSU meta). Exception: "משקל לפי וריאציה" — OCWSU reads _ocwsu_get_weight_from_variation from each variation's weight field.
            var setupTypeNameForVariants = product.SetupType?.Name ?? "";
            var isWeightedBySetupForVariants = setupTypeNameForVariants is "by_weight" or "by_unit" or "by_unit_and_weight";
            var isWeightedForVariations = product.IsWeighted == true || (product.IsWeighted != false && isWeightedBySetupForVariants);
            var weightFromVariation = product.WeightConfig?.WeightByVariant == true
                || string.Equals(product.WeightConfig?.UnitWeightMode?.Name, "by_variant", StringComparison.OrdinalIgnoreCase);

            // Fetch existing WooCommerce variations so we can match by attributes (avoid duplicates) and delete removed ones
            var existingWoo = await GetExistingWooCommerceVariationsAsync(baseUrl, wooProductId, httpClient, cancelToken);
            var existingIdsSet = existingWoo.Select(x => x.id).ToHashSet();
            // signature -> list of Woo variation ids (so we can match one per our variant and delete the rest as duplicates)
            var signatureToIds = existingWoo
                .GroupBy(x => x.signature ?? "")
                .ToDictionary(g => g.Key, g => g.Select(x => x.id).ToList());

            var usedWooVariationIds = new HashSet<int>();
            var wpJsonBaseForMedia = GetWordPressRestBaseUrlFromWooV3BaseUrl(baseUrl);

            // Per-variation stock in Woo whenever George uses stock_management_type "variation" (qty or binary in/out).
            // When VariationStockByQuantity is false, each variant still uses StockQuantity as 0/1 for in/out — we must not send parent stock_status for every line or Woo never reflects per-variation toggles.
            var stockManagedPerVariation = string.Equals(product.StockManagementType?.Name, "variation", StringComparison.OrdinalIgnoreCase);
            var variationTrackQuantity = stockManagedPerVariation && product.VariationStockByQuantity == true;
            var manageVariationStockInWoo = stockManagedPerVariation;
            var productStockStatus = "instock";
            if (product.StockStatus?.Name == "out_of_stock" || product.Status?.Name == "outOfStock")
                productStockStatus = "outofstock";
            else if (product.StockStatus?.Name == "on_backorder")
                productStockStatus = "onbackorder";

            foreach (var variant in variants)
            {
                try
                {
                    var variantStockStatus = manageVariationStockInWoo
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
                    var variationWeightForWoo = isWeightedForVariations && !weightFromVariation
                        ? ""
                        : (variant.Weight.HasValue && variant.Weight.Value > 0
                            ? variant.Weight.Value.ToString(CultureInfo.InvariantCulture)
                            : "");
                    var wooVariation = new Dictionary<string, object>
                    {
                        ["regular_price"] = FormatWooPrice(variant.Price ?? product.Price),
                        ["sku"] = variantWooSku,
                        ["manage_stock"] = manageVariationStockInWoo,
                        ["stock_status"] = variantStockStatus,
                        ["weight"] = variationWeightForWoo,
                        ["attributes"] = variationAttributesList
                    };
                    // Always send sale_price / schedule on PUT so Woo clears stale values when sale is removed or changed per variation.
                    var variationSale = variant.SalePrice;
                    if (!variationSale.HasValue || variationSale.Value <= 0)
                    {
                        // Parent sale applies only when the variant row has no own sale (saved at product level in George).
                        if (product.SalePrice.HasValue && product.SalePrice.Value > 0)
                            variationSale = product.SalePrice;
                    }

                    if (variationSale.HasValue && variationSale.Value > 0)
                    {
                        wooVariation["sale_price"] = FormatWooPrice(variationSale);
                        if (product.SalePriceStartDate.HasValue)
                            wooVariation["date_on_sale_from"] = product.SalePriceStartDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
                        if (product.SalePriceEndDate.HasValue)
                            wooVariation["date_on_sale_to"] = product.SalePriceEndDate.Value.ToString("yyyy-MM-ddTHH:mm:ss");
                    }
                    else
                    {
                        wooVariation["sale_price"] = "";
                        wooVariation["date_on_sale_from"] = "";
                        wooVariation["date_on_sale_to"] = "";
                    }
                    if (manageVariationStockInWoo)
                        wooVariation["stock_quantity"] = ToWooVariationStockQuantity(variant.StockQuantity, variationTrackQuantity);
                    if (!string.IsNullOrEmpty(variant.ImageUrl))
                    {
                        var vUrl = variant.ImageUrl.Trim();
                        if (await ImageRequiresJpegMediaUploadForWooAsync(vUrl, variant.Sku, cancelToken).ConfigureAwait(false))
                        {
                            var compatFile = GeorgeWooVariationImageCompatFileName(product.Id, variant.Id, vUrl);
                            int? mediaId = null;
                            if (wooVariationIdToUse.HasValue)
                                mediaId = await TryGetWooVariationCompatImageMediaIdAsync(baseUrl, wooProductId, wooVariationIdToUse.Value, compatFile, httpClient, cancelToken).ConfigureAwait(false);

                            string? woltSideloadSrc = null;
                            if (!mediaId.HasValue)
                            {
                                try
                                {
                                    var jpegBytes = await DownloadImageAndEncodeAsJpegAsync(vUrl, cancelToken).ConfigureAwait(false);
                                    var uploadResult = await TryUploadJpegToWordPressMediaLibraryAsync(httpClient, wpJsonBaseForMedia, jpegBytes, compatFile, cancelToken).ConfigureAwait(false);
                                    mediaId = uploadResult.MediaId;
                                    if (!mediaId.HasValue && TryAppendWoltJpegFormatQuery(vUrl, out var woltJpegSideloadUrl))
                                    {
                                        _logger.LogInformation(
                                            "Woo sync: wp/v2/media failed ({Status}) for product {ProductId} variant {VariantId}; using Wolt JPEG URL sideload.",
                                            uploadResult.HttpStatus, product.Id, variant.Id);
                                        woltSideloadSrc = woltJpegSideloadUrl;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Woo sync: could not re-encode variation image to JPEG product {ProductId} variant {VariantId}", product.Id, variant.Id);
                                    if (TryAppendWoltJpegFormatQuery(vUrl, out var woltJpegSideloadUrl))
                                    {
                                        _logger.LogInformation(
                                            "Woo sync: using Wolt JPEG URL sideload after decode failure product {ProductId} variant {VariantId}.",
                                            product.Id, variant.Id);
                                        woltSideloadSrc = woltJpegSideloadUrl;
                                    }
                                }
                            }

                            if (mediaId.HasValue)
                                wooVariation["image"] = new { id = mediaId.Value };
                            else if (woltSideloadSrc != null)
                                wooVariation["image"] = new { src = woltSideloadSrc, name = compatFile };
                            else
                                _logger.LogWarning("Woo sync: variation image skipped (JPEG upload path) product {ProductId} variant {VariantId}", product.Id, variant.Id);
                        }
                        else
                        {
                            var variationImageFileName = await ResolveWooImageSideloadFileNameAsync(vUrl, variant.Sku, cancelToken).ConfigureAwait(false);
                            wooVariation["image"] = new { src = vUrl, name = variationImageFileName };
                        }
                    }

                    int? wooVariationId = null;

                    if (wooVariationIdToUse.HasValue)
                    {
                        var updateUrl = $"{baseUrl}/products/{wooProductId}/variations/{wooVariationIdToUse.Value}";
                        var updateJson = JsonSerializer.Serialize(wooVariation, WooVariationJsonOptions);
                        using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
                        var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
                        if (updateResponse.IsSuccessStatusCode)
                        {
                            var updated = await JsonSerializer.DeserializeAsync<WooCommerceVariationResponse>(
                                await updateResponse.Content.ReadAsStreamAsync(cancelToken),
                                cancellationToken: cancelToken);
                            wooVariationId = updated?.id;
                        }
                        else
                        {
                            var err = await updateResponse.Content.ReadAsStringAsync(cancelToken);
                            _logger.LogWarning(
                                "WooCommerce variation PUT failed ({Status}) product {ProductId} variation {WooVariationId}: {Error}",
                                (int)updateResponse.StatusCode, product.Id, wooVariationIdToUse.Value, err);
                        }
                    }

                    if (!wooVariationId.HasValue)
                    {
                        var createUrl = $"{baseUrl}/products/{wooProductId}/variations";
                        var createJson = JsonSerializer.Serialize(wooVariation, WooVariationJsonOptions);
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
        /// <summary>
        /// Maps linked local products to WooCommerce product IDs for upsell_ids / cross_sell_ids (uses WooCommerceId, then SKU lookup).
        /// Order matches the local collection; duplicates are skipped.
        /// </summary>
        private static async Task<List<int>> ResolveWooCommerceIdsForLinkedProductsAsync(
            string baseUrl,
            int siteId,
            ICollection<Product>? linked,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var ordered = new List<int>();
            if (linked == null || linked.Count == 0)
                return ordered;

            var seen = new HashSet<int>();
            foreach (var p in linked)
            {
                if (p == null) continue;
                int? wooId = p.WooCommerceId;
                if (!wooId.HasValue && !string.IsNullOrWhiteSpace(p.Sku))
                    wooId = await FindProductIdBySkuAsync(baseUrl, siteId, p.Sku, httpClient, cancelToken).ConfigureAwait(false);
                if (wooId.HasValue && wooId.Value > 0 && seen.Add(wooId.Value))
                    ordered.Add(wooId.Value);
            }

            return ordered;
        }

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

        private static readonly string[] WooSideloadRecognizedImageExtensions =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".ico", ".svg"
        };

        private static bool FileNameHasRecognizedImageExtension(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            var ext = Path.GetExtension(fileName.Trim());
            return WooSideloadRecognizedImageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);
        }

        private static string SanitizeWooSideloadFileNameStem(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var s = Path.GetFileName(raw.Trim());
            if (string.IsNullOrEmpty(s)) return "";
            s = Regex.Replace(s, @"[^\w\-\.\u0590-\u05FF]+", "_", RegexOptions.CultureInvariant).Trim('_', '.');
            return string.IsNullOrEmpty(s) ? "" : s;
        }

        private static bool FileNamesEqualIgnoringExtension(string a, string b)
        {
            a = Path.GetFileName(a.Trim());
            b = Path.GetFileName(b.Trim());
            if (a.Length == 0 || b.Length == 0) return false;
            return string.Equals(Path.GetFileNameWithoutExtension(a), Path.GetFileNameWithoutExtension(b), StringComparison.OrdinalIgnoreCase);
        }

        private static bool WooProductImageAttachmentNameMatchesHint(string? attachmentName, string? friendlyNameHint, string sideloadFileName)
        {
            if (string.IsNullOrWhiteSpace(attachmentName)) return false;
            attachmentName = attachmentName.Trim();
            if (string.Equals(attachmentName, sideloadFileName, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrWhiteSpace(friendlyNameHint) && string.Equals(attachmentName, friendlyNameHint.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrWhiteSpace(friendlyNameHint) && FileNamesEqualIgnoringExtension(attachmentName, friendlyNameHint.Trim())) return true;
            return FileNamesEqualIgnoringExtension(attachmentName, sideloadFileName);
        }

        /// <summary>
        /// Stable Woo JPEG attachment name per George <see cref="ProductImage.MediaId"/> so replacing a product image uploads fresh bytes
        /// instead of reusing an old attachment matched only by list position (<c>george-woo-{productId}-{index}.jpg</c>).
        /// </summary>
        private static string GeorgeWooProductImageCompatFileName(int productId, int? mediaId, int sortOrder) =>
            mediaId is > 0
                ? $"george-woo-{productId}-m{mediaId.Value}-p{sortOrder}.jpg"
                : $"george-woo-{productId}-p{sortOrder}.jpg";

        private static string GeorgeWooProductImageSideloadFileName(int productId, int? mediaId, string resolvedSideloadFileName)
        {
            if (mediaId is not > 0) return resolvedSideloadFileName;
            var ext = Path.GetExtension(resolvedSideloadFileName);
            if (string.IsNullOrEmpty(ext) || !FileNameHasRecognizedImageExtension(resolvedSideloadFileName))
                ext = ".jpg";
            return $"george-{productId}-m{mediaId.Value}{ext}";
        }

        private static string GeorgeWooVariationImageCompatFileName(int productId, int variantId, string imageUrl)
        {
            var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                Encoding.UTF8.GetBytes(imageUrl.Trim()))[..12]).ToLowerInvariant();
            return $"george-woo-var-{productId}-{variantId}-{fingerprint}.jpg";
        }

        private static (int id, string? src, string? name) FindExistingWooImageByAttachmentName(
            List<(int id, string? src, string? name)>? existingWooImages,
            string attachmentName)
        {
            if (existingWooImages == null || string.IsNullOrWhiteSpace(attachmentName))
                return (0, null, null);
            var match = existingWooImages.FirstOrDefault(ex =>
                string.Equals((ex.name ?? "").Trim(), attachmentName.Trim(), StringComparison.OrdinalIgnoreCase));
            return match.id != 0 ? match : (0, null, null);
        }

        /// <summary>
        /// WooCommerce/WordPress sideload uses the provided <c>name</c> (or URL basename) for file-type checks. URLs without a file extension often fail validation even when bytes are valid JPEG/PNG.
        /// </summary>
        private async Task<string> ResolveWooImageSideloadFileNameAsync(string imageUrl, string? friendlyNameHint, CancellationToken cancelToken)
        {
            var baseFromHint = SanitizeWooSideloadFileNameStem(friendlyNameHint);
            if (string.IsNullOrEmpty(baseFromHint) && Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
            {
                var seg = uri.Segments.LastOrDefault()?.Trim('/');
                baseFromHint = SanitizeWooSideloadFileNameStem(seg);
            }

            if (string.IsNullOrEmpty(baseFromHint))
                baseFromHint = "product-image";

            if (FileNameHasRecognizedImageExtension(baseFromHint))
                return baseFromHint;

            var ext = await ProbeImageExtensionFromUrlAsync(imageUrl, cancelToken).ConfigureAwait(false);
            var stem = Path.GetFileNameWithoutExtension(baseFromHint);
            if (string.IsNullOrEmpty(stem))
                stem = "image";
            return stem + ext;
        }

        /// <summary>
        /// CDNs (e.g. Wolt imageproxy) use <c>Vary: Accept</c> and may return AVIF to default clients; prefer classic raster types for WordPress compatibility.
        /// </summary>
        private static void AddCdnFriendlyImageAcceptHeader(HttpRequestMessage request)
        {
            request.Headers.TryAddWithoutValidation(
                "Accept",
                "image/jpeg,image/png,image/webp,image/gif;q=0.9,image/avif;q=0.3,*/*;q=0.1");
        }

        private async Task<string> ProbeImageExtensionFromUrlAsync(string imageUrl, CancellationToken cancelToken)
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Clear();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, imageUrl);
                AddCdnFriendlyImageAcceptHeader(req);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode && TryMapImageContentTypeToExtension(resp.Content.Headers.ContentType?.MediaType, out var ext))
                    return ext;
            }
            catch
            {
                // fall through to default
            }

            return ".jpg";
        }

        private static bool TryMapImageContentTypeToExtension(string? mediaType, out string extensionWithDot)
        {
            extensionWithDot = ".jpg";
            if (string.IsNullOrWhiteSpace(mediaType)) return false;
            var primary = mediaType.Trim().Split(';')[0].Trim().ToLowerInvariant();
            extensionWithDot = primary switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/pjpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/bmp" or "image/x-ms-bmp" => ".bmp",
                "image/svg+xml" => ".svg",
                "image/x-icon" or "image/vnd.microsoft.icon" => ".ico",
                _ => ""
            };
            return extensionWithDot.Length > 0;
        }

        private static string GetWordPressRestBaseUrlFromWooV3BaseUrl(string wcV3BaseUrl)
        {
            const string suffix = "/wc/v3";
            if (wcV3BaseUrl.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return wcV3BaseUrl[..^suffix.Length];
            var idx = wcV3BaseUrl.LastIndexOf("/wc/v3", StringComparison.OrdinalIgnoreCase);
            return idx >= 0 ? wcV3BaseUrl[..idx] : wcV3BaseUrl;
        }

        private async Task<bool> ImageRequiresJpegMediaUploadForWooAsync(string imageUrl, string? friendlyNameHint, CancellationToken cancelToken)
        {
            // Wolt (and similar) vary real bytes by Accept; WordPress sideload may get WebP/AVIF and reject even with a .jpg filename hint.
            if (ImageUrlUsesWoltStyleNegotiatedCdn(imageUrl))
                return true;
            if (PathOrUriHasAvifHeifHeicExtension(imageUrl) || PathOrUriHasAvifHeifHeicExtension(friendlyNameHint))
                return true;
            if (PathOrUriHasWebpExtension(imageUrl) || PathOrUriHasWebpExtension(friendlyNameHint))
                return true;
            var mime = await ProbeImagePrimaryContentTypeAsync(imageUrl, cancelToken).ConfigureAwait(false);
            return IsAvifHeifHeicContentType(mime) || IsWebpContentType(mime);
        }

        private static bool ImageUrlUsesWoltStyleNegotiatedCdn(string imageUrl)
        {
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var u)) return false;
            return string.Equals(u.Host, "imageproxy.wolt.com", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Wolt imageproxy returns JPEG when <c>format=jpg</c> is set; WordPress can sideload that URL while WooCommerce API keys cannot create <c>wp/v2/media</c> (401).
        /// </summary>
        private static bool TryAppendWoltJpegFormatQuery(string imageUrl, out string jpegSideloadUrl)
        {
            jpegSideloadUrl = imageUrl;
            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var u) || !ImageUrlUsesWoltStyleNegotiatedCdn(imageUrl))
                return false;
            if (imageUrl.Contains("format=", StringComparison.OrdinalIgnoreCase))
            {
                jpegSideloadUrl = imageUrl;
                return true;
            }

            jpegSideloadUrl = string.IsNullOrEmpty(u.Query) ? imageUrl + "?format=jpg" : imageUrl + "&format=jpg";
            return true;
        }

        private static bool PathOrUriHasWebpExtension(string? urlOrFileName)
        {
            if (string.IsNullOrWhiteSpace(urlOrFileName)) return false;
            string ext;
            try
            {
                ext = Uri.TryCreate(urlOrFileName, UriKind.Absolute, out var uri)
                    ? Path.GetExtension(uri.AbsolutePath)
                    : Path.GetExtension(urlOrFileName);
            }
            catch
            {
                ext = Path.GetExtension(urlOrFileName);
            }

            return ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWebpContentType(string? mime)
        {
            if (string.IsNullOrWhiteSpace(mime)) return false;
            var primary = mime.Trim().Split(';')[0].Trim().ToLowerInvariant();
            return primary == "image/webp";
        }

        private static bool PathOrUriHasAvifHeifHeicExtension(string? urlOrFileName)
        {
            if (string.IsNullOrWhiteSpace(urlOrFileName)) return false;
            string ext;
            try
            {
                ext = Uri.TryCreate(urlOrFileName, UriKind.Absolute, out var u)
                    ? Path.GetExtension(u.AbsolutePath)
                    : Path.GetExtension(urlOrFileName);
            }
            catch
            {
                ext = Path.GetExtension(urlOrFileName);
            }

            return ext.Equals(".avif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".heif", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".heic", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsAvifHeifHeicContentType(string? mime)
        {
            if (string.IsNullOrWhiteSpace(mime)) return false;
            var primary = mime.Trim().Split(';')[0].Trim().ToLowerInvariant();
            return primary is "image/avif" or "image/heif" or "image/heic" or "image/heif-sequence" or "image/avif-sequence";
        }

        private async Task<string?> ProbeImagePrimaryContentTypeAsync(string imageUrl, CancellationToken cancelToken)
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);
            client.DefaultRequestHeaders.Clear();
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, imageUrl);
                AddCdnFriendlyImageAcceptHeader(req);
                using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancelToken).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return resp.Content.Headers.ContentType?.MediaType;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        private async Task<byte[]> DownloadImageAndEncodeAsJpegAsync(string imageUrl, CancellationToken cancelToken)
        {
            using var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Clear();
            using var req = new HttpRequestMessage(HttpMethod.Get, imageUrl);
            AddCdnFriendlyImageAcceptHeader(req);
            using var resp = await client.SendAsync(req, HttpCompletionOption.ResponseContentRead, cancelToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using var input = await resp.Content.ReadAsStreamAsync(cancelToken).ConfigureAwait(false);
            using var ms = new MemoryStream();
            await input.CopyToAsync(ms, cancelToken).ConfigureAwait(false);
            using var image = new MagickImage(ms.ToArray());
            image.Format = MagickFormat.Jpeg;
            image.Quality = 90;
            return image.ToByteArray();
        }

        private readonly record struct WpMediaUploadResult(int? MediaId, int HttpStatus);

        private async Task<WpMediaUploadResult> TryUploadJpegToWordPressMediaLibraryAsync(
            HttpClient httpClient,
            string wpJsonBaseUrl,
            byte[] jpegBytes,
            string fileName,
            CancellationToken cancelToken)
        {
            try
            {
                var url = $"{wpJsonBaseUrl.TrimEnd('/')}/wp/v2/media";
                using var multipart = new MultipartFormDataContent();
                var fileContent = new ByteArrayContent(jpegBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                multipart.Add(fileContent, "file", fileName);
                using var resp = await httpClient.PostAsync(url, multipart, cancelToken).ConfigureAwait(false);
                var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                var status = (int)resp.StatusCode;
                if (!resp.IsSuccessStatusCode)
                {
                    var snippet = body.Length > 2000 ? body[..2000] : body;
                    _logger.LogWarning("WordPress POST wp/v2/media failed ({Status}) for {FileName}: {Body}", status, fileName, snippet);
                    return new WpMediaUploadResult(null, status);
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number && idEl.TryGetInt32(out var id))
                    return new WpMediaUploadResult(id, status);

                return new WpMediaUploadResult(null, status);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "WordPress media upload threw for {FileName}", fileName);
                return new WpMediaUploadResult(null, 0);
            }
        }

        /// <summary>
        /// Downloads the external image, encodes as JPEG, uploads via <see cref="IFileStorage"/> (same as media library), updates <see cref="Media"/> and linked <see cref="ProductImage"/> URLs.
        /// </summary>
        private async Task<string?> TryMirrorProductImageToOurStorageForWooAsync(int mediaId, string sourceUrl, CancellationToken cancelToken)
        {
            try
            {
                var jpegBytes = await DownloadImageAndEncodeAsJpegAsync(sourceUrl, cancelToken).ConfigureAwait(false);
                if (jpegBytes.Length == 0) return null;

                var fileName = $"woo-sync-{mediaId}.jpg";
                using var stream = new MemoryStream(jpegBytes);
                var formFile = new FormFile(stream, 0, jpegBytes.Length, "file", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/jpeg"
                };

                var path = FileHelper.GetTempFolderPath();
                var uploadResult = await _fileStorage.UploadFileAsync(formFile, path, cancelToken).ConfigureAwait(false);
                if (!uploadResult.IsSuccessful || string.IsNullOrEmpty(uploadResult.FilePath))
                {
                    _logger.LogWarning(
                        "Woo sync: mirror to our storage failed for media {MediaId}: {Message}",
                        mediaId,
                        uploadResult.Exception?.Message ?? "upload failed");
                    return null;
                }

                var newUrl = FileHelper.GetFileExternalPath(uploadResult.FilePath);
                var updated = await _mediaStorage.UpdateMediaUrlAndSizeAsync(mediaId, newUrl, jpegBytes.LongLength, updateUserId: null, cancelToken).ConfigureAwait(false);
                return updated ? newUrl : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Woo sync: mirror to our storage threw for media {MediaId}, url={Url}", mediaId, sourceUrl);
                return null;
            }
        }

        private static async Task<int?> TryGetWooVariationCompatImageMediaIdAsync(
            string baseUrl,
            int wooProductId,
            int wooVariationId,
            string compatImageFileName,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            try
            {
                var url = $"{baseUrl}/products/{wooProductId}/variations/{wooVariationId}";
                using var resp = await httpClient.GetAsync(url, cancelToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode) return null;
                var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                var v = TryDeserialize<WooVariationReadForImage>(body);
                if (v?.image == null || v.image.id <= 0) return null;
                if (string.Equals((v.image.name ?? "").Trim(), compatImageFileName, StringComparison.OrdinalIgnoreCase))
                    return v.image.id;
            }
            catch
            {
                // ignore
            }

            return null;
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

        /// <summary>GET single variation — image block includes media id and filename for AVIF-compat dedup.</summary>
        private class WooVariationReadForImage
        {
            public WooVariationImageBlock? image { get; set; }
        }

        private class WooVariationImageBlock
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? src { get; set; }
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
        /// Values to register on the Woo global attribute: use <see cref="ProductOptionValue"/> when present;
        /// if empty, derive distinct values from <see cref="ProductVariantOptionValue"/> so variable products
        /// still sync when variants exist but option-value rows were never persisted (API shows empty <c>values</c>).
        /// </summary>
        private static List<string> GetProductOptionValuesForWooSync(ProductOption option, Product product)
        {
            var fromOption = option.ProductOptionValue?
                .Select(pov => pov.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
            if (fromOption.Count > 0)
                return fromOption;

            var key = NormalizeOptionKey(option.Name);
            return product.ProductVariant?
                .Where(v => !v.IsDeleted)
                .SelectMany(v => v.ProductVariantOptionValue ?? Enumerable.Empty<ProductVariantOptionValue>())
                .Where(pvo => NormalizeOptionKey(pvo.OptionName) == key && !string.IsNullOrWhiteSpace(pvo.OptionValue))
                .Select(pvo => pvo.OptionValue.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();
        }

        /// <summary>
        /// OCWSU weighable plugin expects meta <c>_ocwsu_product_weight_units</c> to match radio values: <c>kg</c> or <c>grams</c>.
        /// Our <see cref="Unit.Name"/> may be <c>g</c>, <c>gram</c>, Hebrew <c>גרם</c>, etc.
        /// </summary>
        private static string MapOcwsuProductWeightUnits(string? unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName))
                return "";

            var u = unitName.Trim();
            var lower = u.ToLowerInvariant();
            if (lower is "kg" or "kilogram" or "kilograms")
                return "kg";
            if (string.Equals(u, "ק\"ג", StringComparison.Ordinal))
                return "kg";
            if (string.Equals(u, "גרם", StringComparison.Ordinal))
                return "grams";
            if (lower is "g" or "gram" or "grams")
                return "grams";

            return u;
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

        private static async Task<List<T>> FetchWooPagedAsync<T>(HttpClient httpClient, string endpointUrl, CancellationToken cancelToken)
        {
            var page = 1;
            const int perPage = 100;
            var items = new List<T>();
            while (true)
            {
                var url = endpointUrl.Contains('?')
                    ? $"{endpointUrl}&per_page={perPage}&page={page}"
                    : $"{endpointUrl}?per_page={perPage}&page={page}";
                var response = await httpClient.GetAsync(url, cancelToken);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cancelToken);
                    throw new InvalidOperationException($"WooCommerce fetch failed ({(int)response.StatusCode}) for {url}: {body}");
                }

                var bodyJson = await response.Content.ReadAsStringAsync(cancelToken);
                var pageItems = TryDeserializeFromResponse<List<T>>(bodyJson, url, "GET") ?? new List<T>();

                if (pageItems.Count == 0)
                    break;

                items.AddRange(pageItems);
                if (pageItems.Count < perPage)
                    break;
                page++;
            }
            return items;
        }

        /// <summary>
        /// Woo feed can contain duplicate REST rows for the same product id (bad cache / plugins / double page).
        /// Import stats and progress must follow unique Woo ids so counts match persisted products.
        /// </summary>
        private static List<WooImportProductItem> DedupeWooImportProductsByRestId(List<WooImportProductItem> items)
        {
            return items
                .Where(wp => wp.id > 0)
                .GroupBy(wp => wp.id)
                .Select(g => g.First())
                .ToList();
        }

        /// <summary>Woo ids that appeared more than once in the merged product feed (for support / UI).</summary>
        private static List<WooCommerceImportFeedDuplicateRow> BuildWooProductFeedDuplicateRows(List<WooImportProductItem> raw)
        {
            return raw
                .Where(w => w.id > 0)
                .GroupBy(w => w.id)
                .Where(g => g.Count() > 1)
                .Select(g =>
                {
                    var first = g.First();
                    string? hint = null;
                    if (!string.IsNullOrWhiteSpace(first.name))
                        hint = first.name.Trim();
                    else if (!string.IsNullOrWhiteSpace(first.slug))
                        hint = first.slug.Trim();
                    return new WooCommerceImportFeedDuplicateRow
                    {
                        WooProductId = g.Key,
                        RowCount = g.Count(),
                        NameHint = hint
                    };
                })
                .OrderBy(x => x.WooProductId)
                .ToList();
        }

        /// <summary>Woo sometimes returns blank <c>name</c>; use slug or id so the row is still imported.</summary>
        private static string ResolveWooImportProductDisplayName(WooImportProductItem wp)
        {
            if (!string.IsNullOrWhiteSpace(wp.name))
                return wp.name.Trim();
            if (!string.IsNullOrWhiteSpace(wp.slug))
                return wp.slug.Trim();
            return $"Product #{wp.id}";
        }

        /// <summary>Stable key for matching local categories to Woo rows by parent + name (same name under different parents must not collide).</summary>
        private static string WooImportCategoryCompositeKey(int? parentCategoryId, string nameTrimmed) =>
            $"{(parentCategoryId?.ToString(CultureInfo.InvariantCulture) ?? "root")}\x1e{(nameTrimmed ?? "").Trim()}";

        /// <summary>Parents before children so <see cref="map"/> always contains the Woo parent id when resolving composite keys.</summary>
        private static List<WooImportCategoryItem> OrderWooCategoriesForUpsert(List<WooImportCategoryItem> input)
        {
            var items = input.Where(x => x.id > 0).ToList();
            var idSet = items.Select(x => x.id).ToHashSet();
            var remaining = new HashSet<int>(idSet);
            var result = new List<WooImportCategoryItem>(items.Count);

            while (remaining.Count > 0)
            {
                var batch = items
                    .Where(x =>
                        remaining.Contains(x.id) &&
                        (x.parent <= 0 || !idSet.Contains(x.parent) || !remaining.Contains(x.parent)))
                    .OrderBy(x => x.id)
                    .ToList();

                if (batch.Count == 0)
                {
                    var id = remaining.Min();
                    result.Add(items.First(x => x.id == id));
                    remaining.Remove(id);
                    continue;
                }

                foreach (var x in batch)
                {
                    result.Add(x);
                    remaining.Remove(x.id);
                }
            }

            return result;
        }

        private async Task<Dictionary<int, int>> UpsertCategoriesFromWooAsync(
            GeorgeDBContext db,
            Site site,
            List<WooImportCategoryItem> wooCategories,
            WooCommerceImportFromWooRes stats,
            CancellationToken cancelToken)
        {
            var siteId = site.Id;
            var accountId = site.AccountId;
            // IMPORTANT: unique index is account-wide (Account + Parent + Name), not site-scoped.
            // We must match against all account categories to avoid duplicate-key violations when the
            // same category already exists in the account but is not yet linked to this site.
            var existingCategories = await db.Category
                .Include(c => c.Site)
                .Where(c => !c.IsDeleted && c.AccountId == accountId)
                .ToListAsync(cancelToken);

            var byWooId = existingCategories
                .Where(c => c.WooCommerceId.HasValue)
                .GroupBy(c => c.WooCommerceId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var byComposite = new Dictionary<string, Category>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in existingCategories)
            {
                var key = WooImportCategoryCompositeKey(c.ParentCategoryId, (c.Name ?? string.Empty).Trim());
                if (!byComposite.ContainsKey(key))
                    byComposite[key] = c;
            }

            var orderedWoo = OrderWooCategoriesForUpsert(wooCategories);

            var map = new Dictionary<int, int>();
            foreach (var wc in orderedWoo)
            {
                if (wc.id <= 0 || string.IsNullOrWhiteSpace(wc.name))
                    continue;

                int? parentLocalId = null;
                if (wc.parent > 0 && map.TryGetValue(wc.parent, out var pLocal))
                    parentLocalId = pLocal;

                var compositeKey = WooImportCategoryCompositeKey(parentLocalId, wc.name.Trim());

                Category? category = null;
                if (byWooId.TryGetValue(wc.id, out var existingByWoo))
                    category = existingByWoo;
                else if (byComposite.TryGetValue(compositeKey, out var existingByComposite))
                    category = existingByComposite;

                if (category == null)
                {
                    category = new Category
                    {
                        AccountId = accountId,
                        Name = wc.name.Trim(),
                        ParentCategoryId = parentLocalId,
                        Description = wc.description,
                        WooCommerceId = wc.id,
                        IsActive = true,
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                        GuidId = Guid.NewGuid()
                    };
                    category.Site.Add(site);
                    db.Category.Add(category);
                    stats.Categories.Created++;
                }
                else
                {
                    category.Name = wc.name.Trim();
                    category.ParentCategoryId = parentLocalId;
                    category.Description = wc.description;
                    category.WooCommerceId = wc.id;
                    category.IsDeleted = false;
                    category.UpdatedDate = DateTime.UtcNow;
                    if (!category.Site.Any(s => s.Id == siteId))
                        category.Site.Add(site);
                    stats.Categories.Updated++;
                }

                await db.SaveChangesAsync(cancelToken);
                map[wc.id] = category.Id;
                byWooId[wc.id] = category;
                byComposite[compositeKey] = category;
            }

            foreach (var wc in wooCategories)
            {
                if (wc.id <= 0 || wc.parent <= 0) continue;
                if (!map.TryGetValue(wc.id, out var childId) || !map.TryGetValue(wc.parent, out var parentId)) continue;
                var child = await db.Category.FirstOrDefaultAsync(c => c.Id == childId, cancelToken);
                if (child != null && child.ParentCategoryId != parentId)
                {
                    child.ParentCategoryId = parentId;
                    child.UpdatedDate = DateTime.UtcNow;
                }
            }
            await db.SaveChangesAsync(cancelToken);
            return map;
        }

        private async Task UpsertProductsFromWooAsync(
            GeorgeDBContext db,
            Site site,
            string baseUrl,
            HttpClient httpClient,
            List<WooImportProductItem> wooProducts,
            Dictionary<int, int> categoryMap,
            Dictionary<int, int> brandMap,
            WooImportCatalogLookups importLookups,
            WooCommerceImportFromWooRes stats,
            IProgress<WooCommerceImportProgress>? importProgress,
            CancellationToken cancelToken)
        {
            var siteId = site.Id;
            var accountId = site.AccountId;
            // Match only catalog rows already on THIS site. Same SKU/name/Woo id on another site → not in this set → import creates a new Product and links it here (multi-site catalog).
            var existingProducts = await db.Product
                .Include(p => p.Site)
                .Include(p => p.ProductCategory)
                .Include(p => p.Tag)
                .Include(p => p.ProductImage)
                .Include(p => p.ProductOption).ThenInclude(po => po.ProductOptionValue)
                .Include(p => p.ProductVariant).ThenInclude(v => v.ProductVariantOptionValue)
                .Where(p => !p.IsDeleted && p.AccountId == accountId && p.Site.Any(s => s.Id == siteId))
                .ToListAsync(cancelToken);

            var byWooId = existingProducts
                .Where(p => p.WooCommerceId.HasValue)
                .GroupBy(p => p.WooCommerceId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var bySku = existingProducts
                .Where(p => !string.IsNullOrWhiteSpace(p.Sku))
                .GroupBy(p => p.Sku!.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var eligibleProducts = wooProducts.Where(wp => wp.id > 0).ToList();
            // Progress matches what we actually persist: one step per distinct Woo product id (duplicate rows in the feed are merged upstream).
            var productTotal = eligibleProducts.Count;
            importProgress?.Report(new WooCommerceImportProgress { Phase = "products", Total = productTotal, Completed = 0 });
            var completedProducts = 0;
            /** One Created/Updated stat per local Product.Id (two Woo parents can share SKU → same row; duplicate ids already stripped upstream). */
            var importProductStatsIds = new HashSet<int>();

            for (var offset = 0; offset < eligibleProducts.Count; offset += WooImportProductBatchSize)
            {
                cancelToken.ThrowIfCancellationRequested();
                var batch = eligibleProducts.Skip(offset).Take(WooImportProductBatchSize).ToList();
                var batchEnd = offset + batch.Count;
                _logger.LogInformation(
                    "WooCommerce import: processing products {From}-{To} of {Total}",
                    offset + 1,
                    batchEnd,
                    productTotal);

                var variationMap = await PrefetchWooImportVariationsAsync(
                    httpClient,
                    baseUrl,
                    batch,
                    WooImportVariationPrefetchParallelism,
                    cancelToken);

                foreach (var wp in batch)
                {
                    if (!variationMap.TryGetValue(wp.id, out var wooVariations))
                        wooVariations = new List<WooImportVariationItem>();

                    Product? product = null;
                    if (byWooId.TryGetValue(wp.id, out var existingByWoo))
                        product = existingByWoo;
                    else if (!string.IsNullOrWhiteSpace(wp.sku) && bySku.TryGetValue(wp.sku.Trim(), out var existingBySku))
                    {
                        // Two different Woo ids must not share one local row (duplicate SKUs in Woo are common).
                        // SKU match only when this row is still unlinked to Woo, or already this Woo id.
                        if (existingBySku.WooCommerceId == null || existingBySku.WooCommerceId == wp.id)
                            product = existingBySku;
                    }

                    bool isCreate = product == null;
                    if (isCreate)
                    {
                        product = new Product
                        {
                            AccountId = accountId,
                            GuidId = Guid.NewGuid(),
                            CreationTime = DateTime.UtcNow,
                            IsDeleted = false,
                            IsActive = true
                        };
                        product.Site.Add(site);
                        db.Product.Add(product);
                    }
                    else
                    {
                        if (!product!.Site.Any(s => s.Id == siteId))
                            product.Site.Add(site);
                        product.IsDeleted = false;
                        product.UpdatedDate = DateTime.UtcNow;
                    }

                    if (product == null)
                        continue;

                    // Rare: two Woo rows resolved to the same tracked local row in one run (e.g. odd DB state). Skipping drops Woo ids from the catalog — always persist one row per Woo id.
                    if (product.Id != 0 && importProductStatsIds.Contains(product.Id))
                    {
                        _logger.LogWarning(
                            "WooCommerce import: Woo product id {WooId} (SKU {Sku}) resolved to local product {LocalId} already updated in this run — creating a new local product for this Woo id.",
                            wp.id,
                            wp.sku ?? "",
                            product.Id);
                        product = new Product
                        {
                            AccountId = accountId,
                            GuidId = Guid.NewGuid(),
                            CreationTime = DateTime.UtcNow,
                            IsDeleted = false,
                            IsActive = true
                        };
                        product.Site.Add(site);
                        db.Product.Add(product);
                        isCreate = true;
                    }

                    product.Name = ResolveWooImportProductDisplayName(wp);
                    product.ShortDescription = wp.short_description;
                    product.LongDescription = wp.description;
                    product.Sku = string.IsNullOrWhiteSpace(wp.sku) ? null : wp.sku.Trim();
                    product.WooCommerceId = wp.id;
                    product.Price = ParseNullableDecimal(wp.regular_price);
                    product.SalePrice = ParseNullableDecimal(wp.sale_price);
                    product.Weight = ParseNullableDecimal(wp.weight);
                    product.StockQuantity = wp.manage_stock == true ? wp.stock_quantity : null;

                    await db.SaveChangesAsync(cancelToken);

                    if (importProductStatsIds.Add(product.Id))
                    {
                        if (isCreate)
                            stats.Products.Created++;
                        else
                            stats.Products.Updated++;
                    }

                    db.ProductCategory.RemoveRange(db.ProductCategory.Where(x => x.ProductId == product.Id));
                    var optionIds = product.ProductOption.Select(o => o.Id).ToList();
                    var variantIds = product.ProductVariant.Select(v => v.Id).ToList();
                    if (variantIds.Count > 0)
                    {
                        db.ProductVariantOptionValue.RemoveRange(db.ProductVariantOptionValue.Where(x => variantIds.Contains(x.ProductVariantId)));
                        db.ProductVariant.RemoveRange(db.ProductVariant.Where(x => variantIds.Contains(x.Id)));
                    }
                    if (optionIds.Count > 0)
                    {
                        db.ProductOptionValue.RemoveRange(db.ProductOptionValue.Where(x => optionIds.Contains(x.ProductOptionId)));
                        db.ProductOption.RemoveRange(db.ProductOption.Where(x => optionIds.Contains(x.Id)));
                    }

                    if (wp.categories != null)
                    {
                        foreach (var wc in wp.categories)
                        {
                            if (wc.id > 0 && categoryMap.TryGetValue(wc.id, out var localCategoryId))
                                db.ProductCategory.Add(new ProductCategory { ProductId = product.Id, CategoryId = localCategoryId });
                        }
                    }

                    var optionNameToValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                    if (wp.attributes != null)
                    {
                        foreach (var attr in wp.attributes.Where(a => !string.IsNullOrWhiteSpace(a.name)))
                        {
                            if (!optionNameToValues.TryGetValue(attr.name!.Trim(), out var values))
                            {
                                values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                                optionNameToValues[attr.name.Trim()] = values;
                            }
                            foreach (var value in attr.options ?? new List<string>())
                            {
                                if (!string.IsNullOrWhiteSpace(value))
                                    values.Add(value.Trim());
                            }
                        }
                    }

                    foreach (var kv in optionNameToValues)
                    {
                        var po = new ProductOption { ProductId = product.Id, Name = kv.Key, IsDeleted = false };
                        db.ProductOption.Add(po);
                        await db.SaveChangesAsync(cancelToken);
                        foreach (var value in kv.Value)
                            db.ProductOptionValue.Add(new ProductOptionValue { ProductOptionId = po.Id, Value = value });
                    }

                    foreach (var vv in wooVariations)
                    {
                        var variant = new ProductVariant
                        {
                            ProductId = product.Id,
                            WooCommerceVariationId = vv.id,
                            Sku = string.IsNullOrWhiteSpace(vv.sku) ? null : vv.sku.Trim(),
                            Price = ParseNullableDecimal(vv.regular_price),
                            SalePrice = ParseNullableDecimal(vv.sale_price),
                            Weight = ParseNullableDecimal(vv.weight),
                            StockQuantity = ResolveImportedVariantStockQuantity(vv),
                            ImageUrl = vv.image?.src,
                            IsDeleted = false
                        };
                        db.ProductVariant.Add(variant);
                        await db.SaveChangesAsync(cancelToken);

                        foreach (var a in vv.attributes ?? new List<WooImportVariationAttributeItem>())
                        {
                            if (string.IsNullOrWhiteSpace(a.name) || string.IsNullOrWhiteSpace(a.option))
                                continue;
                            db.ProductVariantOptionValue.Add(new ProductVariantOptionValue
                            {
                                ProductVariantId = variant.Id,
                                OptionName = a.name.Trim(),
                                OptionValue = a.option.Trim()
                            });
                        }
                    }
                    stats.Variations.Updated += wooVariations.Count;

                    // George UI: sum/qty column only when VariationStockByQuantity is true (matches Woo per-variation manage_stock / qty).
                    if (wooVariations.Count > 0)
                    {
                        product.VariationStockByQuantity = wooVariations.Any(v =>
                            v.manage_stock == true
                            || (v.stock_quantity.HasValue && v.stock_quantity.Value > 0));
                    }
                    else
                    {
                        product.VariationStockByQuantity = null;
                    }

                    await db.SaveChangesAsync(cancelToken);

                    // WooCommerce variable products usually omit parent regular_price; UI and list need a parent price.
                    if (wooVariations.Count > 0)
                    {
                        var variationRegular = wooVariations
                            .Select(v => ParseNullableDecimal(v.regular_price))
                            .Where(p => p.HasValue)
                            .Select(p => p!.Value)
                            .ToList();
                        if (variationRegular.Count > 0 && product.Price == null)
                            product.Price = variationRegular.Min();

                        var variationSale = wooVariations
                            .Select(v => ParseNullableDecimal(v.sale_price))
                            .Where(p => p.HasValue && p.Value > 0)
                            .Select(p => p!.Value)
                            .ToList();
                        if (variationSale.Count > 0 && product.SalePrice == null)
                            product.SalePrice = variationSale.Min();
                    }

                    await db.SaveChangesAsync(cancelToken);

                    await ApplyWooImportProductExtensionsAsync(db, product, wp, accountId, siteId, importLookups, brandMap, cancelToken);

                    // Woo variable parent can be "out of stock" in REST while each variation is instock without manage_stock.
                    if (wooVariations.Count > 0)
                    {
                        var outStockId = ResolveStockStatusId(importLookups, "outofstock");
                        var inStockId = ResolveStockStatusId(importLookups, "instock");
                        var anySalable = wooVariations.Any(v =>
                        {
                            var q = ResolveImportedVariantStockQuantity(v);
                            return q.HasValue && q.Value > 0;
                        });
                        if (anySalable && inStockId.HasValue && outStockId.HasValue && product.StockStatusId == outStockId.Value)
                        {
                            product.StockStatusId = inStockId.Value;
                            await db.SaveChangesAsync(cancelToken);
                        }
                    }

                    byWooId[wp.id] = product;
                    if (!string.IsNullOrWhiteSpace(product.Sku))
                        bySku[product.Sku.Trim()] = product;

                    completedProducts++;
                    importProgress?.Report(new WooCommerceImportProgress { Phase = "products", Total = productTotal, Completed = completedProducts });
                }
            }
        }

        private static decimal? ParseNullableDecimal(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            if (decimal.TryParse(raw.Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
                return value;
            return null;
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

        private class WooImportCategoryItem
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? description { get; set; }
            public int parent { get; set; }
        }

        private class WooImportProductCategoryItem
        {
            public int id { get; set; }
        }

        private class WooImportProductAttributeItem
        {
            public string? name { get; set; }
            public bool variation { get; set; }
            public List<string>? options { get; set; }
        }

        /// <summary>
        /// WooCommerce may return manage_stock as bool, number, or string values like "yes"/"no"/"parent".
        /// Keep import resilient by accepting these variants.
        /// </summary>
        private sealed class FlexibleNullableBoolConverter : JsonConverter<bool?>
        {
            public override bool? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null) return null;
                if (reader.TokenType == JsonTokenType.True) return true;
                if (reader.TokenType == JsonTokenType.False) return false;

                if (reader.TokenType == JsonTokenType.Number)
                {
                    if (reader.TryGetInt32(out var n)) return n != 0;
                    return null;
                }

                if (reader.TokenType == JsonTokenType.String)
                {
                    var s = reader.GetString()?.Trim();
                    if (string.IsNullOrEmpty(s)) return null;

                    if (string.Equals(s, "true", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s, "1", StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (string.Equals(s, "false", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s, "no", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(s, "0", StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (string.Equals(s, "parent", StringComparison.OrdinalIgnoreCase))
                        return null;
                }

                return null;
            }

            public override void Write(Utf8JsonWriter writer, bool? value, JsonSerializerOptions options)
            {
                if (value.HasValue) writer.WriteBooleanValue(value.Value);
                else writer.WriteNullValue();
            }
        }

        private class WooImportProductItem
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? slug { get; set; }
            public string? description { get; set; }
            public string? short_description { get; set; }
            public string? sku { get; set; }
            public string? type { get; set; }
            public string? status { get; set; }
            public string? catalog_visibility { get; set; }
            public string? stock_status { get; set; }
            public int menu_order { get; set; }
            public string? shipping_class { get; set; }
            public string? regular_price { get; set; }
            public string? sale_price { get; set; }
            public string? date_on_sale_from { get; set; }
            public string? date_on_sale_from_gmt { get; set; }
            public string? date_on_sale_to { get; set; }
            public string? date_on_sale_to_gmt { get; set; }
            public string? weight { get; set; }
            [JsonConverter(typeof(FlexibleNullableBoolConverter))]
            public bool? manage_stock { get; set; }
            public decimal? stock_quantity { get; set; }
            public List<WooImportProductCategoryItem>? categories { get; set; }
            public List<WooImportProductAttributeItem>? attributes { get; set; }
            public List<WooImportMetaEntry>? meta_data { get; set; }
            public List<WooImportImageListItem>? images { get; set; }
            public List<WooImportTagItem>? tags { get; set; }
            /// <summary>
            /// WooCommerce 9.6+ brands on the product. REST read/write both use an array of objects with <c>id</c>
            /// (see Products API); import only needs this read shape.
            /// </summary>
            public List<WooImportProductBrandItem>? brands { get; set; }

            /// <summary>WooCommerce REST: linked up-sell product IDs (מוצרים משודרגים).</summary>
            public List<int>? upsell_ids { get; set; }

            /// <summary>WooCommerce REST: linked cross-sell product IDs (מוצרים משלימים).</summary>
            public List<int>? cross_sell_ids { get; set; }
        }

        /// <summary>One entry inside the product's brands[] array on read.</summary>
        private class WooImportProductBrandItem
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? slug { get; set; }
        }

        private class WooImportImageItem
        {
            public string? src { get; set; }
        }

        private class WooImportVariationAttributeItem
        {
            public string? name { get; set; }
            public string? option { get; set; }
        }

        private class WooImportVariationItem
        {
            public int id { get; set; }
            public string? sku { get; set; }
            public string? regular_price { get; set; }
            public string? sale_price { get; set; }
            public string? weight { get; set; }
            [JsonConverter(typeof(FlexibleNullableBoolConverter))]
            public bool? manage_stock { get; set; }
            public decimal? stock_quantity { get; set; }
            /// <summary>WooCommerce: instock | outofstock | onbackorder (hyphenated in some payloads).</summary>
            public string? stock_status { get; set; }
            public WooImportImageItem? image { get; set; }
            public List<WooImportVariationAttributeItem>? attributes { get; set; }
        }
    }
}

