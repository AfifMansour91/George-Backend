using AutoMapper;
using George.Common;
using George.Data;
using George.Services.Request;
using George.Services.Response;
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

        public ProductSiteOverrideService(
            ILogger<ProductSiteOverrideService> logger,
            IMapper mapper,
            CacheManager cache,
            ProductSiteOverrideStorage overrideStorage,
            ProductStorage productStorage
        ) : base(logger, mapper, cache)
        {
            _overrideStorage = overrideStorage;
            _productStorage = productStorage;
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

        public async Task<IApiResponse<bool>> UpdateVariantStockAsync(int productId, ProductSiteVariantStockReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();
            var items = req.Items.Select(i => (i.VariantId, i.StockQuantity, i.StockStatus));
            await _overrideStorage.UpsertVariantStockAsync(productId, req.SiteId, items, cancelToken);
            response.Data = true;
            return CreateResponse(response);
        }
    }
}
