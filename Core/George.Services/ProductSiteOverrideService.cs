using AutoMapper;
using George.Common;
using George.Data;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    /// <summary>
    /// MultiSite Phase 2 — per-site override operations: upsert/reset a site override, exclude/include a
    /// product from network management at a site, list local/excluded products, and per-site variant stock.
    /// </summary>
    public class ProductSiteOverrideService : ServiceBase
    {
        private readonly ProductSiteOverrideStorage _overrideStorage;
        private readonly ProductStorage _productStorage;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ProductSiteOverrideService(
            ILogger<ProductSiteOverrideService> logger,
            IMapper mapper,
            CacheManager cache,
            ProductSiteOverrideStorage overrideStorage,
            ProductStorage productStorage,
            IServiceScopeFactory serviceScopeFactory
        ) : base(logger, mapper, cache)
        {
            _overrideStorage = overrideStorage;
            _productStorage = productStorage;
            _serviceScopeFactory = serviceScopeFactory;
        }

        /// <summary>
        /// Fire-and-forget WooCommerce sync of one product to ONE site after a per-site override change.
        /// Every write in this service (override upsert/reset, variant stock, exclude/include) must push the
        /// site's new effective values to that site's Woo store — without this, per-branch stock/price edits
        /// from the "הצג" dialog saved to the DB but the website never updated (until an unrelated edit
        /// happened to re-sync the product). Resolves its OWN scope: the request scope is disposed by the
        /// time the task runs (same lesson as the promotion webhook dispatcher).
        /// </summary>
        private void QueueSiteSync(int productId, int siteId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var siteStorage = scope.ServiceProvider.GetRequiredService<SiteStorage>();
                    var site = await siteStorage.GetSiteAsync(siteId, CancellationToken.None);
                    if (site == null || site.WooCommerceEnabled != true)
                        return;

                    var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
                    var syncReq = new WooCommerceSyncReq
                    {
                        SiteId = siteId,
                        ProductIds = new List<int> { productId }
                    };
                    var syncResponse = await wooCommerceService.SyncToWooCommerceAsync(syncReq, CancellationToken.None);
                    if (syncResponse.Data?.Success == null || !syncResponse.Data.Success.Any())
                    {
                        _logger.LogWarning(
                            "Per-site override change: failed to sync product {ProductId} to WooCommerce for site {SiteId}: {Message}",
                            productId, siteId, syncResponse.Data?.Message ?? "Unknown error");
                    }
                }
                catch (Exception ex)
                {
                    // Log but never throw — Woo sync failures must not surface into the override save.
                    _logger.LogError(ex,
                        "Per-site override change: error syncing product {ProductId} to WooCommerce for site {SiteId}",
                        productId, siteId);
                }
            }, CancellationToken.None);
        }

        public async Task<IApiResponse<ProductSiteOverrideRes>> UpsertOverrideAsync(
            int productId, int siteId, ProductSiteOverrideReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductSiteOverrideRes>();

            var product = await _productStorage.GetProductAsync(productId, cancelToken);
            if (product == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // A product managed locally on another site cannot be edited from this site.
            if (product.ManagementMode == "local" && product.OwnerSiteId.HasValue && product.OwnerSiteId.Value != siteId)
                return CreateResponse(response, StatusCode.InvalidRequest, "Product is local to another site and cannot be edited here.");

            var row = await _overrideStorage.UpsertOverrideAsync(
                productId, siteId, product.AccountId,
                req.IsExcluded, req.Price, req.SalePrice, req.SalePriceStartDate, req.SalePriceEndDate,
                req.Availability, req.StockManagementType, req.StockStatus, req.StockQuantity,
                req.VariationStockByQuantity, req.LowStockThreshold, cancelToken,
                name: req.Name, shortDescription: req.ShortDescription, longDescription: req.LongDescription,
                weight: req.Weight, weightUnit: req.WeightUnit, sku: req.Sku,
                seoTitle: req.SeoTitle, seoDescription: req.SeoDescription);

            response.Data = new ProductSiteOverrideRes
            {
                Id = row.Id,
                ProductId = row.ProductId,
                SiteId = row.SiteId,
                AccountId = row.AccountId,
                IsExcluded = row.IsExcluded,
                Price = row.Price,
                SalePrice = row.SalePrice,
                SalePriceStartDate = row.SalePriceStartDate,
                SalePriceEndDate = row.SalePriceEndDate,
                Availability = row.Availability,
                StockManagementType = req.StockManagementType,
                StockStatus = req.StockStatus,
                StockQuantity = row.StockQuantity,
                VariationStockByQuantity = row.VariationStockByQuantity,
                LowStockThreshold = row.LowStockThreshold,
            };

            // Push the branch's new effective values to this site's Woo store.
            QueueSiteSync(productId, siteId);
            return CreateResponse(response);
        }

        public async Task<IApiResponse<bool>> ResetOverrideAsync(
            int productId, int siteId, string? fieldsCsv, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();
            var fields = string.IsNullOrWhiteSpace(fieldsCsv)
                ? null
                : fieldsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            await _overrideStorage.ResetOverrideAsync(productId, siteId, fields, cancelToken);
            response.Data = true;

            // The site now inherits the canonical values again — push them to its Woo store.
            QueueSiteSync(productId, siteId);
            return CreateResponse(response);
        }

        public async Task<IApiResponse<bool>> ExcludeAsync(int productId, ProductSiteScopeReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();
            var product = await _productStorage.GetProductAsync(productId, cancelToken);
            if (product == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            await _overrideStorage.SetExcludedAsync(productId, req.SiteId, product.AccountId, excluded: true, resetFields: false, cancelToken);
            response.Data = true;

            // Push the exclusion to the site's Woo store (the sync handles excluded products for the site).
            QueueSiteSync(productId, req.SiteId);
            return CreateResponse(response);
        }

        public async Task<IApiResponse<bool>> IncludeAsync(int productId, ProductSiteScopeReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();
            var product = await _productStorage.GetProductAsync(productId, cancelToken);
            if (product == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            await _overrideStorage.SetExcludedAsync(productId, req.SiteId, product.AccountId, excluded: false, resetFields: req.ResetFields, cancelToken);
            response.Data = true;

            // The product is back under network management at this site — push its effective values to Woo.
            QueueSiteSync(productId, req.SiteId);
            return CreateResponse(response);
        }

        public async Task<IApiResponse<LocalProductsRes>> ListLocalAsync(int accountId, int? siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<LocalProductsRes> { Data = new LocalProductsRes() };
            var rows = await _overrideStorage.ListLocalAsync(accountId, siteId, cancelToken);
            response.Data.Items = rows
                .Select(r => new LocalProductEntryRes { ProductId = r.ProductId, Name = r.Name, SiteId = r.SiteId, Reason = r.Reason })
                .ToList();
            response.Data.Total = response.Data.Items.Count;
            return CreateResponse(response);
        }

        /// <summary>For the given products, which have a per-site override on price / sku / stock (drives "הצג").</summary>
        public async Task<IApiResponse<List<ProductFieldOverrideFlagsRes>>> GetFieldOverrideFlagsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<ProductFieldOverrideFlagsRes>>();
            var rows = await _overrideStorage.GetFieldOverrideFlagsAsync(productIds, cancelToken);
            response.Data = rows.Select(r => new ProductFieldOverrideFlagsRes
            {
                ProductId = r.ProductId,
                PriceOverridden = r.PriceOverridden,
                SkuOverridden = r.SkuOverridden,
                StockOverridden = r.StockOverridden,
            }).ToList();
            return CreateResponse(response);
        }

        /// <summary>Per-site price/sku/stock for a product (for the per-branch edit popup).</summary>
        public async Task<IApiResponse<ProductSiteFieldValuesRes>> GetSiteFieldValuesAsync(int productId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductSiteFieldValuesRes>();
            var data = await _overrideStorage.GetSiteFieldValuesAsync(productId, cancelToken);
            if (data == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            response.Data = new ProductSiteFieldValuesRes
            {
                ProductId = data.ProductId,
                BasePrice = data.BasePrice,
                BaseSku = data.BaseSku,
                BaseStock = data.BaseStock,
                Sites = data.Sites.Select(s => new SiteFieldValueRes
                {
                    SiteId = s.SiteId,
                    SiteName = s.SiteName,
                    Price = s.Price,
                    Sku = s.Sku,
                    StockQuantity = s.StockQuantity,
                    PriceOverridden = s.PriceOverridden,
                    SkuOverridden = s.SkuOverridden,
                    StockOverridden = s.StockOverridden,
                }).ToList(),
            };
            return CreateResponse(response);
        }

        public async Task<IApiResponse<bool>> UpdateVariantStockAsync(int productId, ProductSiteVariantStockReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();
            var items = req.Items.Select(i => (i.VariantId, i.StockQuantity, i.StockStatus));
            await _overrideStorage.UpsertVariantStockAsync(productId, req.SiteId, items, cancelToken);
            response.Data = true;

            // Push the branch's new variant stock to this site's Woo store.
            QueueSiteSync(productId, req.SiteId);
            return CreateResponse(response);
        }
    }
}
