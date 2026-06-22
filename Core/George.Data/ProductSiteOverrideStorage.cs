using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    /// <summary>Resolved per-site override values for a product (lookup ids already mapped to names).</summary>
    public sealed class SiteOverrideValues
    {
        public int ProductId { get; set; }
        public bool IsExcluded { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public DateTime? SalePriceStartDate { get; set; }
        public DateTime? SalePriceEndDate { get; set; }
        public decimal? StockQuantity { get; set; }
        public bool? VariationStockByQuantity { get; set; }
        public decimal? LowStockThreshold { get; set; }
        public string? StockStatus { get; set; }
        public string? StockManagementType { get; set; }
    }

    /// <summary>
    /// MultiSite Phase 2 — data access for the per-site product override layer
    /// (ProductSiteOverride + ProductSiteVariantStock).
    /// </summary>
    public class ProductSiteOverrideStorage : StorageBase
    {
        public ProductSiteOverrideStorage(GeorgeDBContext dbContext, ILogger<ProductSiteOverrideStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<ProductSiteOverride?> GetOverrideAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.ProductSiteOverride
                .FirstOrDefaultAsync(o => o.ProductId == productId && o.SiteId == siteId, cancelToken);
        }

        public async Task<List<ProductSiteOverride>> GetOverridesForProductAsync(int productId, CancellationToken cancelToken)
        {
            return await _dbContext.ProductSiteOverride
                .Where(o => o.ProductId == productId)
                .ToListAsync(cancelToken);
        }

        /// <summary>
        /// Resolved per-site override values for a batch of products at one site (for list "effective view").
        /// Stock status/management-type ids are mapped to their names.
        /// </summary>
        public async Task<List<SiteOverrideValues>> GetOverridesForSiteAsync(IReadOnlyCollection<int> productIds, int siteId, CancellationToken cancelToken)
        {
            if (productIds == null || productIds.Count == 0) return new List<SiteOverrideValues>();
            return await _dbContext.ProductSiteOverride
                .Where(o => o.SiteId == siteId && productIds.Contains(o.ProductId))
                .Select(o => new SiteOverrideValues
                {
                    ProductId = o.ProductId,
                    IsExcluded = o.IsExcluded,
                    Price = o.Price,
                    SalePrice = o.SalePrice,
                    SalePriceStartDate = o.SalePriceStartDate,
                    SalePriceEndDate = o.SalePriceEndDate,
                    StockQuantity = o.StockQuantity,
                    VariationStockByQuantity = o.VariationStockByQuantity,
                    LowStockThreshold = o.LowStockThreshold,
                    StockStatus = _dbContext.StockStatus.Where(s => s.Id == o.StockStatusId).Select(s => s.Name).FirstOrDefault(),
                    StockManagementType = _dbContext.StockManagementType.Where(s => s.Id == o.StockManagementTypeId).Select(s => s.Name).FirstOrDefault(),
                })
                .ToListAsync(cancelToken);
        }

        /// <summary>Per-site stock for a product's variants at one site: variantId → stockQuantity.</summary>
        public async Task<Dictionary<int, decimal?>> GetVariantStockForSiteAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            var rows = await _dbContext.ProductSiteVariantStock
                .Where(v => v.SiteId == siteId && v.ProductId == productId)
                .Select(v => new { v.ProductVariantId, v.StockQuantity })
                .ToListAsync(cancelToken);
            var map = new Dictionary<int, decimal?>();
            foreach (var r in rows) map[r.ProductVariantId] = r.StockQuantity;
            return map;
        }

        /// <summary>Loads the override row, creating an empty one if absent (not yet saved).</summary>
        private async Task<ProductSiteOverride> GetOrCreateAsync(int productId, int siteId, int? accountId, CancellationToken cancelToken)
        {
            var existing = await GetOverrideAsync(productId, siteId, cancelToken);
            if (existing != null) return existing;

            var created = new ProductSiteOverride
            {
                ProductId = productId,
                SiteId = siteId,
                AccountId = accountId,
                CreationTime = DateTime.UtcNow,
            };
            _dbContext.ProductSiteOverride.Add(created);
            return created;
        }

        private async Task<int?> ResolveStockStatusIdAsync(string? name, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var ss = await _dbContext.StockStatus.FirstOrDefaultAsync(s => s.Name == name, cancelToken);
            return ss?.Id;
        }

        private async Task<int?> ResolveStockManagementTypeIdAsync(string? name, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var smt = await _dbContext.StockManagementType.FirstOrDefaultAsync(s => s.Name == name && !s.IsDeleted, cancelToken);
            return smt?.Id;
        }

        /// <summary>
        /// Upsert a sparse override. Only non-null inputs are written; nulls keep the current override value.
        /// Stock status/management-type names are resolved to lookup ids.
        /// </summary>
        public async Task<ProductSiteOverride> UpsertOverrideAsync(
            int productId,
            int siteId,
            int? accountId,
            bool? isExcluded,
            decimal? price,
            decimal? salePrice,
            DateTime? salePriceStartDate,
            DateTime? salePriceEndDate,
            bool? availability,
            string? stockManagementType,
            string? stockStatus,
            decimal? stockQuantity,
            bool? variationStockByQuantity,
            decimal? lowStockThreshold,
            CancellationToken cancelToken)
        {
            var row = await GetOrCreateAsync(productId, siteId, accountId, cancelToken);

            if (isExcluded.HasValue) row.IsExcluded = isExcluded.Value;
            if (price.HasValue) row.Price = price;
            if (salePrice.HasValue) row.SalePrice = salePrice;
            if (salePriceStartDate.HasValue) row.SalePriceStartDate = salePriceStartDate;
            if (salePriceEndDate.HasValue) row.SalePriceEndDate = salePriceEndDate;
            if (availability.HasValue) row.Availability = availability;
            if (stockManagementType.HasValue()) row.StockManagementTypeId = await ResolveStockManagementTypeIdAsync(stockManagementType, cancelToken);
            if (stockStatus.HasValue()) row.StockStatusId = await ResolveStockStatusIdAsync(stockStatus, cancelToken);
            if (stockQuantity.HasValue) row.StockQuantity = stockQuantity;
            if (variationStockByQuantity.HasValue) row.VariationStockByQuantity = variationStockByQuantity;
            if (lowStockThreshold.HasValue) row.LowStockThreshold = lowStockThreshold.Value <= 0 ? (decimal?)null : lowStockThreshold.Value;

            row.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return row;
        }

        /// <summary>Resets selected override fields (or all but IsExcluded when fields is null/empty) back to inheriting the canonical.</summary>
        public async Task ResetOverrideAsync(int productId, int siteId, IReadOnlyCollection<string>? fields, CancellationToken cancelToken)
        {
            var row = await GetOverrideAsync(productId, siteId, cancelToken);
            if (row == null) return;

            bool All(string f) => fields == null || fields.Count == 0 || fields.Contains(f);

            if (All("price")) { row.Price = null; }
            if (All("salePrice")) { row.SalePrice = null; row.SalePriceStartDate = null; row.SalePriceEndDate = null; }
            if (All("availability")) { row.Availability = null; }
            if (All("stock"))
            {
                row.StockManagementTypeId = null;
                row.StockStatusId = null;
                row.StockQuantity = null;
                row.VariationStockByQuantity = null;
                row.LowStockThreshold = null;
            }
            row.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        public async Task SetExcludedAsync(int productId, int siteId, int? accountId, bool excluded, bool resetFields, CancellationToken cancelToken)
        {
            var row = await GetOrCreateAsync(productId, siteId, accountId, cancelToken);
            row.IsExcluded = excluded;
            if (resetFields)
            {
                row.Price = null; row.SalePrice = null; row.SalePriceStartDate = null; row.SalePriceEndDate = null;
                row.Availability = null; row.StockManagementTypeId = null; row.StockStatusId = null;
                row.StockQuantity = null; row.VariationStockByQuantity = null; row.LowStockThreshold = null;
            }
            else if (excluded)
            {
                // Snapshot the canonical product's overridable fields into this site's override so the site
                // keeps its current values and is immune to future all-sites (canonical) edits.
                var product = await _dbContext.Product.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, cancelToken);
                if (product != null)
                {
                    if (row.Price == null) row.Price = product.Price;
                    if (row.SalePrice == null) row.SalePrice = product.SalePrice;
                    if (row.SalePriceStartDate == null) row.SalePriceStartDate = product.SalePriceStartDate;
                    if (row.SalePriceEndDate == null) row.SalePriceEndDate = product.SalePriceEndDate;
                    if (row.StockManagementTypeId == null) row.StockManagementTypeId = product.StockManagementTypeId;
                    if (row.StockStatusId == null) row.StockStatusId = product.StockStatusId;
                    if (row.StockQuantity == null) row.StockQuantity = product.StockQuantity;
                    if (row.VariationStockByQuantity == null) row.VariationStockByQuantity = product.VariationStockByQuantity;
                    if (row.LowStockThreshold == null) row.LowStockThreshold = product.LowStockThreshold;
                }
            }
            row.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>
        /// Local + excluded products for an account (optionally a single site): products that are
        /// managed locally (ManagementMode = 'local') or excluded from the network at some site.
        /// </summary>
        public async Task<List<(int ProductId, string Name, int SiteId, string Reason)>> ListLocalAsync(int accountId, int? siteId, CancellationToken cancelToken)
        {
            var result = new List<(int, string, int, string)>();

            // Excluded overrides — filter by the product's account (robust even if the override's
            // denormalized AccountId was not set).
            var excludedQuery = _dbContext.ProductSiteOverride
                .Where(o => o.IsExcluded)
                .Join(_dbContext.Product, o => o.ProductId, p => p.Id,
                    (o, p) => new { o.ProductId, p.Name, o.SiteId, ProductAccountId = p.AccountId })
                .Where(x => x.ProductAccountId == accountId);
            if (siteId.HasValue) excludedQuery = excludedQuery.Where(x => x.SiteId == siteId.Value);
            var excluded = await excludedQuery.ToListAsync(cancelToken);
            result.AddRange(excluded.Select(e => (e.ProductId, e.Name, e.SiteId, "excluded")));

            // Local products (managed only on their owner site)
            var localQuery = _dbContext.Product
                .Where(p => p.AccountId == accountId && p.ManagementMode == "local" && p.OwnerSiteId != null);
            if (siteId.HasValue) localQuery = localQuery.Where(p => p.OwnerSiteId == siteId.Value);
            var local = await localQuery
                .Select(p => new { p.Id, p.Name, OwnerSiteId = p.OwnerSiteId!.Value })
                .ToListAsync(cancelToken);
            result.AddRange(local.Select(l => (l.Id, l.Name, l.OwnerSiteId, "local")));

            return result;
        }

        /// <summary>Upsert per-site stock for a set of variants.</summary>
        public async Task UpsertVariantStockAsync(int productId, int siteId, IEnumerable<(int VariantId, decimal? StockQuantity, string? StockStatus)> items, CancellationToken cancelToken)
        {
            foreach (var item in items)
            {
                var row = await _dbContext.ProductSiteVariantStock
                    .FirstOrDefaultAsync(v => v.ProductVariantId == item.VariantId && v.SiteId == siteId, cancelToken);
                if (row == null)
                {
                    row = new ProductSiteVariantStock
                    {
                        ProductVariantId = item.VariantId,
                        SiteId = siteId,
                        ProductId = productId,
                        CreationTime = DateTime.UtcNow,
                    };
                    _dbContext.ProductSiteVariantStock.Add(row);
                }
                if (item.StockQuantity.HasValue) row.StockQuantity = item.StockQuantity;
                if (item.StockStatus.HasValue()) row.StockStatusId = await ResolveStockStatusIdAsync(item.StockStatus, cancelToken);
                row.UpdatedDate = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }
    }
}
