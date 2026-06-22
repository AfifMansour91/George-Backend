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
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public string? Sku { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
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

        /// <summary>
        /// True when the site's account is network-managed (so per-site overrides may exist). Mirrors the
        /// frontend resolution: explicit ManagementMode='network', or legacy null + WizardType='all_sites'.
        /// Used to skip override lookups entirely for non-network (e.g. single-site) accounts.
        /// </summary>
        public async Task<bool> IsSiteNetworkManagedAsync(int siteId, CancellationToken cancelToken)
        {
            var info = await _dbContext.Site
                .Where(s => s.Id == siteId)
                .Select(s => new
                {
                    s.Account.ManagementMode,
                    WizardType = s.Account.WizardType != null ? s.Account.WizardType.Name : null
                })
                .FirstOrDefaultAsync(cancelToken);
            if (info == null) return false;
            if (info.ManagementMode == "network") return true;
            if (string.IsNullOrEmpty(info.ManagementMode) && info.WizardType == "all_sites") return true;
            return false;
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
                    Name = o.Name,
                    ShortDescription = o.ShortDescription,
                    LongDescription = o.LongDescription,
                    Weight = o.Weight,
                    WeightUnit = o.WeightUnit,
                    Sku = o.Sku,
                    SeoTitle = o.SeoTitle,
                    SeoDescription = o.SeoDescription,
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

        /// <summary>All non-deleted site ids for an account (used to exclude a site-only variant from the other sites).</summary>
        public async Task<List<int>> GetAccountSiteIdsAsync(int accountId, CancellationToken cancelToken)
        {
            return await _dbContext.Site
                .Where(s => s.AccountId == accountId && !s.IsDeleted)
                .Select(s => s.Id)
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

        /// <summary>Resolved per-site override values for a single variant (null field = inherit canonical).</summary>
        public sealed class VariantSiteOverride
        {
            public decimal? StockQuantity { get; set; }
            public string? StockStatus { get; set; }
            public decimal? Price { get; set; }
            public decimal? SalePrice { get; set; }
            public bool IsExcluded { get; set; }
        }

        /// <summary>Full per-site variant overrides for a product at one site: variantId → override.</summary>
        public async Task<Dictionary<int, VariantSiteOverride>> GetVariantOverridesForSiteAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            var rows = await _dbContext.ProductSiteVariantStock
                .Where(v => v.SiteId == siteId && v.ProductId == productId)
                .Select(v => new { v.ProductVariantId, v.StockQuantity, v.StockStatusId, v.Price, v.SalePrice, v.IsExcluded })
                .ToListAsync(cancelToken);
            // Resolve stock status ids to names in one pass.
            var statusIds = rows.Where(r => r.StockStatusId.HasValue).Select(r => r.StockStatusId!.Value).Distinct().ToList();
            var statusNames = statusIds.Count == 0
                ? new Dictionary<int, string>()
                : await _dbContext.StockStatus.Where(s => statusIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name, cancelToken);
            var map = new Dictionary<int, VariantSiteOverride>();
            foreach (var r in rows)
            {
                map[r.ProductVariantId] = new VariantSiteOverride
                {
                    StockQuantity = r.StockQuantity,
                    StockStatus = r.StockStatusId.HasValue && statusNames.TryGetValue(r.StockStatusId.Value, out var n) ? n : null,
                    Price = r.Price,
                    SalePrice = r.SalePrice,
                    IsExcluded = r.IsExcluded,
                };
            }
            return map;
        }

        /// <summary>Batched per-site variant overrides for many products at one site: productId → (variantId → override).</summary>
        public async Task<Dictionary<int, Dictionary<int, VariantSiteOverride>>> GetVariantOverridesForSiteAsync(IReadOnlyCollection<int> productIds, int siteId, CancellationToken cancelToken)
        {
            var result = new Dictionary<int, Dictionary<int, VariantSiteOverride>>();
            if (productIds == null || productIds.Count == 0) return result;
            var rows = await _dbContext.ProductSiteVariantStock
                .Where(v => v.SiteId == siteId && v.ProductId != null && productIds.Contains(v.ProductId.Value))
                .Select(v => new { v.ProductId, v.ProductVariantId, v.StockQuantity, v.StockStatusId, v.Price, v.SalePrice, v.IsExcluded })
                .ToListAsync(cancelToken);
            var statusIds = rows.Where(r => r.StockStatusId.HasValue).Select(r => r.StockStatusId!.Value).Distinct().ToList();
            var statusNames = statusIds.Count == 0
                ? new Dictionary<int, string>()
                : await _dbContext.StockStatus.Where(s => statusIds.Contains(s.Id))
                    .ToDictionaryAsync(s => s.Id, s => s.Name, cancelToken);
            foreach (var r in rows)
            {
                if (!result.TryGetValue(r.ProductId!.Value, out var inner))
                {
                    inner = new Dictionary<int, VariantSiteOverride>();
                    result[r.ProductId.Value] = inner;
                }
                inner[r.ProductVariantId] = new VariantSiteOverride
                {
                    StockQuantity = r.StockQuantity,
                    StockStatus = r.StockStatusId.HasValue && statusNames.TryGetValue(r.StockStatusId.Value, out var n) ? n : null,
                    Price = r.Price,
                    SalePrice = r.SalePrice,
                    IsExcluded = r.IsExcluded,
                };
            }
            return result;
        }

        /// <summary>Per-site variant override upsert payload.</summary>
        public sealed class VariantSiteOverrideUpsert
        {
            public int VariantId { get; set; }
            public decimal? Price { get; set; }
            public decimal? SalePrice { get; set; }
            public decimal? StockQuantity { get; set; }
            public string? StockStatus { get; set; }
            public bool IsExcluded { get; set; }
        }

        /// <summary>
        /// Upsert per-site variant overrides (price/sale/stock/exclusion). Each item fully describes the variant's
        /// state for this site; Price/SalePrice/StockQuantity are assigned as given (null clears back to inherit).
        /// </summary>
        public async Task UpsertVariantOverridesAsync(int productId, int siteId, IEnumerable<VariantSiteOverrideUpsert> items, CancellationToken cancelToken)
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
                row.Price = item.Price;
                row.SalePrice = item.SalePrice;
                if (item.StockQuantity.HasValue) row.StockQuantity = item.StockQuantity;
                if (item.StockStatus.HasValue()) row.StockStatusId = await ResolveStockStatusIdAsync(item.StockStatus, cancelToken);
                row.IsExcluded = item.IsExcluded;
                row.UpdatedDate = DateTime.UtcNow;
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>Per-site category assignment for a product (with parent id so callers can split main/sub).</summary>
        public sealed class SiteCategoryAssignment
        {
            public int ProductId { get; set; }
            public int CategoryId { get; set; }
            public int? ParentCategoryId { get; set; }
        }

        /// <summary>Per-site category assignments for a batch of products at one site (empty = inherit canonical).</summary>
        public async Task<List<SiteCategoryAssignment>> GetSiteCategoriesAsync(IReadOnlyCollection<int> productIds, int siteId, CancellationToken cancelToken)
        {
            if (productIds == null || productIds.Count == 0) return new List<SiteCategoryAssignment>();
            return await _dbContext.ProductSiteCategory
                .Where(x => x.SiteId == siteId && productIds.Contains(x.ProductId))
                .Join(_dbContext.Category, x => x.CategoryId, c => c.Id,
                    (x, c) => new SiteCategoryAssignment { ProductId = x.ProductId, CategoryId = x.CategoryId, ParentCategoryId = c.ParentCategoryId })
                .ToListAsync(cancelToken);
        }

        /// <summary>Replace a product's per-site category assignment for one site.</summary>
        public async Task SetSiteCategoriesAsync(int productId, int siteId, IReadOnlyCollection<int> categoryIds, CancellationToken cancelToken)
        {
            var existing = await _dbContext.ProductSiteCategory
                .Where(x => x.ProductId == productId && x.SiteId == siteId)
                .ToListAsync(cancelToken);
            _dbContext.ProductSiteCategory.RemoveRange(existing);
            foreach (var cid in categoryIds.Where(c => c > 0).Distinct())
            {
                _dbContext.ProductSiteCategory.Add(new ProductSiteCategory { ProductId = productId, SiteId = siteId, CategoryId = cid });
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>Per-site image rows for a batch of products at one site (ordered by SortOrder). Empty = inherit canonical.</summary>
        public async Task<List<(int ProductId, string Url, int SortOrder)>> GetSiteImagesAsync(IReadOnlyCollection<int> productIds, int siteId, CancellationToken cancelToken)
        {
            if (productIds == null || productIds.Count == 0) return new List<(int, string, int)>();
            var rows = await _dbContext.ProductSiteImage
                .Where(x => x.SiteId == siteId && productIds.Contains(x.ProductId))
                .OrderBy(x => x.SortOrder)
                .Select(x => new { x.ProductId, x.Url, x.SortOrder })
                .ToListAsync(cancelToken);
            return rows.Select(r => (r.ProductId, r.Url, r.SortOrder)).ToList();
        }

        /// <summary>Replace a product's per-site images for one site (urls in display order).</summary>
        public async Task SetSiteImagesAsync(int productId, int siteId, IReadOnlyList<string> urls, CancellationToken cancelToken)
        {
            var existing = await _dbContext.ProductSiteImage
                .Where(x => x.ProductId == productId && x.SiteId == siteId)
                .ToListAsync(cancelToken);
            _dbContext.ProductSiteImage.RemoveRange(existing);
            for (var i = 0; i < urls.Count; i++)
            {
                var url = urls[i]?.Trim();
                if (string.IsNullOrEmpty(url)) continue;
                _dbContext.ProductSiteImage.Add(new ProductSiteImage { ProductId = productId, SiteId = siteId, Url = url, SortOrder = i });
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>The WooCommerce product id for this product in this site's store (null if not yet synced there).</summary>
        public async Task<int?> GetSiteWooProductIdAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.ProductSiteWooId
                .Where(x => x.ProductId == productId && x.SiteId == siteId)
                .Select(x => (int?)x.WooCommerceProductId)
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>Upsert the per-site WooCommerce product id for a (product, site).</summary>
        public async Task SetSiteWooProductIdAsync(int productId, int siteId, int wooProductId, CancellationToken cancelToken)
        {
            var row = await _dbContext.ProductSiteWooId
                .FirstOrDefaultAsync(x => x.ProductId == productId && x.SiteId == siteId, cancelToken);
            if (row == null)
            {
                row = new ProductSiteWooId { ProductId = productId, SiteId = siteId, WooCommerceProductId = wooProductId };
                _dbContext.ProductSiteWooId.Add(row);
            }
            else
            {
                row.WooCommerceProductId = wooProductId;
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>localCategoryId -> this site's WooCommerce category id, for categories already synced to this store.</summary>
        public async Task<Dictionary<int, int>> GetSiteCategoryWooIdMapAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.CategorySiteWooId
                .Where(x => x.SiteId == siteId)
                .ToDictionaryAsync(x => x.CategoryId, x => x.WooCommerceCategoryId, cancelToken);
        }

        /// <summary>Upsert the per-site WooCommerce category id for a (category, site).</summary>
        public async Task SetSiteCategoryWooIdAsync(int categoryId, int siteId, int wooCategoryId, CancellationToken cancelToken)
        {
            var row = await _dbContext.CategorySiteWooId
                .FirstOrDefaultAsync(x => x.CategoryId == categoryId && x.SiteId == siteId, cancelToken);
            if (row == null)
            {
                row = new CategorySiteWooId { CategoryId = categoryId, SiteId = siteId, WooCommerceCategoryId = wooCategoryId };
                _dbContext.CategorySiteWooId.Add(row);
            }
            else
            {
                row.WooCommerceCategoryId = wooCategoryId;
            }
            await _dbContext.SaveChangesAsync(cancelToken);
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
            CancellationToken cancelToken,
            // Per-site scalar field overrides (null = don't change).
            string? name = null,
            string? shortDescription = null,
            string? longDescription = null,
            decimal? weight = null,
            string? weightUnit = null,
            string? sku = null,
            string? seoTitle = null,
            string? seoDescription = null)
        {
            var row = await GetOrCreateAsync(productId, siteId, accountId, cancelToken);

            if (isExcluded.HasValue) row.IsExcluded = isExcluded.Value;
            if (name != null) row.Name = name;
            if (shortDescription != null) row.ShortDescription = shortDescription;
            if (longDescription != null) row.LongDescription = longDescription;
            if (weight.HasValue) row.Weight = weight;
            if (weightUnit != null) row.WeightUnit = weightUnit;
            if (sku != null) row.Sku = sku;
            if (seoTitle != null) row.SeoTitle = seoTitle;
            if (seoDescription != null) row.SeoDescription = seoDescription;
            if (price.HasValue) row.Price = price;
            if (salePrice.HasValue) row.SalePrice = salePrice;
            if (salePriceStartDate.HasValue) row.SalePriceStartDate = salePriceStartDate;
            if (salePriceEndDate.HasValue) row.SalePriceEndDate = salePriceEndDate;
            if (availability.HasValue) row.Availability = availability;
            if (stockManagementType.HasValue())
            {
                var smtId = await ResolveStockManagementTypeIdAsync(stockManagementType, cancelToken);
                if (smtId.HasValue) row.StockManagementTypeId = smtId; // don't null out on an unresolvable name
            }
            if (stockStatus.HasValue())
            {
                var ssId = await ResolveStockStatusIdAsync(stockStatus, cancelToken);
                if (ssId.HasValue) row.StockStatusId = ssId;
            }
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
                    if (row.Name == null) row.Name = product.Name;
                    if (row.ShortDescription == null) row.ShortDescription = product.ShortDescription;
                    if (row.LongDescription == null) row.LongDescription = product.LongDescription;
                    if (row.Weight == null) row.Weight = product.Weight;
                    if (row.WeightUnit == null) row.WeightUnit = product.WeightUnit;
                    if (row.Sku == null) row.Sku = product.Sku;
                    if (row.SeoTitle == null) row.SeoTitle = product.SeoTitle;
                    if (row.SeoDescription == null) row.SeoDescription = product.SeoDescription;
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
