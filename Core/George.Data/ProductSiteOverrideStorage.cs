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

        // Per-site merchandising overrides (lookup ids resolved to names; null = inherit canonical).
        public decimal? CostPrice { get; set; }
        public bool? IsKosher { get; set; }
        public string? Status { get; set; }
        public string? Visibility { get; set; }
        public string? Slug { get; set; }
        public string? ShippingClass { get; set; }
        public string? Supplier { get; set; }
        public bool? LabelFrozen { get; set; }
        public bool? LabelGlutenFree { get; set; }
        public bool? LabelNotKosher { get; set; }
        public bool? LabelKosherForPassover { get; set; }
        public DateTime? LabelKosherForPassoverEndDate { get; set; }
        public bool? LabelNew { get; set; }
        public DateTime? LabelNewEndDate { get; set; }
        public bool? LabelBestseller { get; set; }
        public bool? LabelLowAvailability { get; set; }
        public bool? LabelReadyToCook { get; set; }
        public bool? LabelNatural { get; set; }
        public bool? LabelSugarFree { get; set; }
        public bool? LabelLactoseFree { get; set; }
    }

    /// <summary>
    /// MultiSite Phase 2 - data access for the per-site product override layer
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
                    CostPrice = o.CostPrice,
                    IsKosher = o.IsKosher,
                    Status = _dbContext.ProductStatus.Where(s => s.Id == o.StatusId).Select(s => s.Name).FirstOrDefault(),
                    Visibility = _dbContext.Visibility.Where(v => v.Id == o.VisibilityId).Select(v => v.Name).FirstOrDefault(),
                    Slug = o.Slug,
                    ShippingClass = _dbContext.ShippingClass.Where(s => s.Id == o.ShippingClassId).Select(s => s.Name).FirstOrDefault(),
                    Supplier = _dbContext.Supplier.Where(s => s.Id == o.SupplierId).Select(s => s.Name).FirstOrDefault(),
                    LabelFrozen = o.LabelFrozen,
                    LabelGlutenFree = o.LabelGlutenFree,
                    LabelNotKosher = o.LabelNotKosher,
                    LabelKosherForPassover = o.LabelKosherForPassover,
                    LabelKosherForPassoverEndDate = o.LabelKosherForPassoverEndDate,
                    LabelNew = o.LabelNew,
                    LabelNewEndDate = o.LabelNewEndDate,
                    LabelBestseller = o.LabelBestseller,
                    LabelLowAvailability = o.LabelLowAvailability,
                    LabelReadyToCook = o.LabelReadyToCook,
                    LabelNatural = o.LabelNatural,
                    LabelSugarFree = o.LabelSugarFree,
                    LabelLactoseFree = o.LabelLactoseFree,
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

        /// <summary>Which of price/sku/stock a product has a per-site override for (any site). Drives the "הצג" affordance.</summary>
        public sealed class ProductFieldOverrideFlags
        {
            public int ProductId { get; set; }
            public bool PriceOverridden { get; set; }
            public bool SkuOverridden { get; set; }
            public bool StockOverridden { get; set; }
        }

        /// <summary>
        /// For a batch of products: whether price / sku / stock actually DIFFER across the branches the product is
        /// on. Drives the "הצג" affordance. Bug #13: this must reflect a real difference between branches, NOT merely
        /// "an override row exists" - otherwise setting the same value on every branch (an override that equals the
        /// canonical) left the badge on. The effective value per site = its override when set, else the canonical
        /// product value; the flag is true only when more than one distinct effective value exists across the sites.
        /// </summary>
        public async Task<List<ProductFieldOverrideFlags>> GetFieldOverrideFlagsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancelToken)
        {
            if (productIds == null || productIds.Count == 0) return new List<ProductFieldOverrideFlags>();

            var products = await _dbContext.Product
                .AsNoTracking()
                .Where(p => !p.IsDeleted && productIds.Contains(p.Id))
                .Select(p => new
                {
                    p.Id,
                    p.Price,
                    p.Sku,
                    p.StockQuantity,
                    SiteIds = p.Site.Select(s => s.Id).ToList(),
                })
                .ToListAsync(cancelToken);

            var overrides = await _dbContext.ProductSiteOverride
                .AsNoTracking()
                .Where(o => !o.IsDeleted && productIds.Contains(o.ProductId))
                .Select(o => new { o.ProductId, o.SiteId, o.Price, o.Sku, o.StockQuantity })
                .ToListAsync(cancelToken);
            var ovrByKey = overrides
                .GroupBy(o => (o.ProductId, o.SiteId))
                .ToDictionary(g => g.Key, g => g.First());

            var result = new List<ProductFieldOverrideFlags>();
            foreach (var p in products)
            {
                // A single-branch (or unassigned) product can't differ between branches → no badge.
                if (p.SiteIds.Count <= 1) continue;

                var priceVals = new HashSet<decimal?>();
                var skuVals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var stockVals = new HashSet<decimal?>();
                foreach (var sid in p.SiteIds)
                {
                    ovrByKey.TryGetValue((p.Id, sid), out var o);
                    priceVals.Add(o?.Price ?? p.Price);
                    skuVals.Add((!string.IsNullOrEmpty(o?.Sku) ? o!.Sku : p.Sku) ?? "");
                    stockVals.Add(o?.StockQuantity ?? p.StockQuantity);
                }

                var flags = new ProductFieldOverrideFlags
                {
                    ProductId = p.Id,
                    PriceOverridden = priceVals.Count > 1,
                    SkuOverridden = skuVals.Count > 1,
                    StockOverridden = stockVals.Count > 1,
                };
                if (flags.PriceOverridden || flags.SkuOverridden || flags.StockOverridden)
                    result.Add(flags);
            }
            return result;
        }

        /// <summary>One site's effective price/sku/stock for a product, with flags for which are per-site overrides.</summary>
        public sealed class SiteFieldValueRow
        {
            public int SiteId { get; set; }
            public string SiteName { get; set; } = "";
            public decimal? Price { get; set; }
            public string? Sku { get; set; }
            public decimal? StockQuantity { get; set; }
            public bool PriceOverridden { get; set; }
            public bool SkuOverridden { get; set; }
            public bool StockOverridden { get; set; }
        }

        /// <summary>Per-site price/sku/stock for a product across all its sites, plus the canonical base values.</summary>
        public sealed class ProductSiteFieldValues
        {
            public int ProductId { get; set; }
            public decimal? BasePrice { get; set; }
            public string? BaseSku { get; set; }
            public decimal? BaseStock { get; set; }
            public List<SiteFieldValueRow> Sites { get; set; } = new List<SiteFieldValueRow>();
        }

        /// <summary>Builds the per-site field view for the "edit per branch" popup (effective = override ?? canonical).</summary>
        public async Task<ProductSiteFieldValues?> GetSiteFieldValuesAsync(int productId, CancellationToken cancelToken)
        {
            var product = await _dbContext.Product
                .Where(p => p.Id == productId)
                .Select(p => new { p.Price, p.Sku, p.StockQuantity })
                .FirstOrDefaultAsync(cancelToken);
            if (product == null) return null;

            var sites = await _dbContext.Product
                .Where(p => p.Id == productId)
                .SelectMany(p => p.Site)
                .Where(s => !s.IsDeleted)
                .Select(s => new { s.Id, s.SiteName })
                .ToListAsync(cancelToken);

            var overrides = await _dbContext.ProductSiteOverride
                .Where(o => !o.IsDeleted && o.ProductId == productId)
                .Select(o => new { o.SiteId, o.Price, o.Sku, o.StockQuantity })
                .ToListAsync(cancelToken);
            var ovrBySite = overrides.GroupBy(o => o.SiteId).ToDictionary(g => g.Key, g => g.First());

            var result = new ProductSiteFieldValues
            {
                ProductId = productId,
                BasePrice = product.Price,
                BaseSku = product.Sku,
                BaseStock = product.StockQuantity,
            };
            foreach (var s in sites.OrderBy(s => s.Id))
            {
                ovrBySite.TryGetValue(s.Id, out var o);
                result.Sites.Add(new SiteFieldValueRow
                {
                    SiteId = s.Id,
                    SiteName = string.IsNullOrEmpty(s.SiteName) ? s.Id.ToString() : s.SiteName,
                    Price = o?.Price ?? product.Price,
                    Sku = string.IsNullOrEmpty(o?.Sku) ? product.Sku : o!.Sku,
                    StockQuantity = o?.StockQuantity ?? product.StockQuantity,
                    PriceOverridden = o?.Price != null,
                    SkuOverridden = !string.IsNullOrEmpty(o?.Sku),
                    StockOverridden = o?.StockQuantity != null,
                });
            }
            return result;
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

        /// <summary>
        /// The George product currently linked to a WooCommerce product id on this site: the per-site map
        /// (ProductSiteWooId) first, then the legacy single Product.WooCommerceId column for products on the
        /// site. Returns null when no George product claims that Woo product. Used to detect when a SKU lookup
        /// would adopt a Woo product already owned by a DIFFERENT product (e.g. two products duplicated from one
        /// source share "{sku}-copy"), which would make both products overwrite a single Woo product.
        /// </summary>
        public async Task<int?> GetProductIdBySiteWooProductIdAsync(int siteId, int wooProductId, CancellationToken cancelToken)
        {
            // Active products only: a claim left behind by a soft-deleted product (catalog re-imports
            // replace whole generations) must not block a live product from adopting its Woo id.
            var bySiteMap = await _dbContext.ProductSiteWooId
                .Where(x => x.SiteId == siteId && x.WooCommerceProductId == wooProductId)
                .Join(
                    _dbContext.Product.Where(p => !p.IsDeleted),
                    x => x.ProductId,
                    p => p.Id,
                    (x, p) => (int?)p.Id)
                .FirstOrDefaultAsync(cancelToken);
            if (bySiteMap.HasValue) return bySiteMap;

            return await _dbContext.Product
                .Where(p => !p.IsDeleted && p.WooCommerceId == wooProductId && p.Site.Any(s => s.Id == siteId))
                .Select(p => (int?)p.Id)
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>Upsert the per-site WooCommerce product id for a (product, site).</summary>
        public async Task SetSiteWooProductIdAsync(int productId, int siteId, int wooProductId, CancellationToken cancelToken)
        {
            // A Woo product has at most ONE George owner per site. Stale claims by soft-deleted products
            // are removed here (self-heal after re-imports) so this write cannot create a duplicate claim;
            // a conflicting claim by an ACTIVE product is a real fault and is left for the unique index
            // (UX_ProductSiteWooId_Site_WooProduct) to reject loudly instead of poisoning order resolution.
            var staleClaims = await _dbContext.ProductSiteWooId
                .Where(x => x.SiteId == siteId && x.WooCommerceProductId == wooProductId && x.ProductId != productId)
                .Join(
                    _dbContext.Product.Where(p => p.IsDeleted),
                    x => x.ProductId,
                    p => p.Id,
                    (x, p) => x)
                .ToListAsync(cancelToken);
            if (staleClaims.Count > 0)
                _dbContext.ProductSiteWooId.RemoveRange(staleClaims);

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

        /// <summary>
        /// variantId -> this site's Woo VARIATION id, for a product's variants in this site's store. The single
        /// ProductVariant.WooCommerceVariationId column can only hold one site's id, so a multi-site variable
        /// product must resolve variation ids per (variant, site) from here (see ProductSiteVariantWooId).
        /// </summary>
        public async Task<Dictionary<int, int>> GetSiteVariantWooIdMapAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.ProductSiteVariantWooId
                .Where(x => x.ProductId == productId && x.SiteId == siteId)
                .ToDictionaryAsync(x => x.ProductVariantId, x => x.WooCommerceVariationId, cancelToken);
        }

        /// <summary>Upsert the per-site WooCommerce variation id for a (variant, site).</summary>
        public async Task SetSiteVariantWooIdAsync(int productVariantId, int siteId, int productId, int wooVariationId, CancellationToken cancelToken)
        {
            var row = await _dbContext.ProductSiteVariantWooId
                .FirstOrDefaultAsync(x => x.ProductVariantId == productVariantId && x.SiteId == siteId, cancelToken);
            if (row == null)
            {
                row = new ProductSiteVariantWooId { ProductVariantId = productVariantId, SiteId = siteId, ProductId = productId, WooCommerceVariationId = wooVariationId };
                _dbContext.ProductSiteVariantWooId.Add(row);
            }
            else
            {
                row.WooCommerceVariationId = wooVariationId;
                row.ProductId = productId;
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>All (wooProductId → productId) links for this site from the per-site map (bulk, for the external price pull).</summary>
        public async Task<Dictionary<int, int>> GetSiteWooProductIdMapForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var rows = await _dbContext.ProductSiteWooId
                .Where(x => x.SiteId == siteId)
                .Select(x => new { x.WooCommerceProductId, x.ProductId })
                .ToListAsync(cancelToken);
            var map = new Dictionary<int, int>();
            foreach (var r in rows) map[r.WooCommerceProductId] = r.ProductId;
            return map;
        }

        /// <summary>
        /// External price pull (Woo → George), network-managed site: write the store's current prices into this
        /// site's per-site override. Unlike <see cref="UpsertOverrideAsync"/> (sparse), sale fields are ASSIGNED
        /// as given - a sale removed at the POS clears the override's sale (note: a null override sale still
        /// inherits the canonical sale at sync/display time; that inherit semantics is a known limitation).
        /// A missing override row is only materialized when the store's price actually differs from canonical,
        /// so one pull does not mark every product on the site as price-overridden.
        /// Returns true when anything was written.
        /// </summary>
        public async Task<bool> UpsertExternalPricesAsync(
            int productId,
            int siteId,
            int? accountId,
            decimal? price,
            decimal? salePrice,
            DateTime? salePriceStartDate,
            DateTime? salePriceEndDate,
            CancellationToken cancelToken)
        {
            var row = await GetOverrideAsync(productId, siteId, cancelToken);
            if (row == null)
            {
                // All-null incoming values (e.g. a Woo variable parent reports no own prices) would only
                // materialize an empty override row that inherits canonical anyway - skip.
                if (!price.HasValue && !salePrice.HasValue && !salePriceStartDate.HasValue && !salePriceEndDate.HasValue)
                    return false;
                var canonical = await _dbContext.Product.AsNoTracking()
                    .Where(p => p.Id == productId)
                    .Select(p => new { p.Price, p.SalePrice, p.SalePriceStartDate, p.SalePriceEndDate })
                    .FirstOrDefaultAsync(cancelToken);
                if (canonical == null) return false;
                var differs = (price.HasValue && canonical.Price != price)
                    || canonical.SalePrice != salePrice
                    || canonical.SalePriceStartDate != salePriceStartDate
                    || canonical.SalePriceEndDate != salePriceEndDate;
                if (!differs) return false;
                row = new ProductSiteOverride
                {
                    ProductId = productId,
                    SiteId = siteId,
                    AccountId = accountId,
                    CreationTime = DateTime.UtcNow,
                };
                _dbContext.ProductSiteOverride.Add(row);
            }

            var changed = row.Id == 0; // freshly added row
            if (price.HasValue && row.Price != price) { row.Price = price; changed = true; }
            if (row.SalePrice != salePrice) { row.SalePrice = salePrice; changed = true; }
            if (row.SalePriceStartDate != salePriceStartDate) { row.SalePriceStartDate = salePriceStartDate; changed = true; }
            if (row.SalePriceEndDate != salePriceEndDate) { row.SalePriceEndDate = salePriceEndDate; changed = true; }
            if (!changed) return false;

            row.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// External price pull (Woo → George), network-managed site: write the store's current variation prices
        /// into this site's per-variant overrides. Only touches Price/SalePrice (never stock/exclusion). Missing
        /// rows are only materialized when the store's price differs from the canonical variant. Returns the
        /// number of variant rows written.
        /// </summary>
        public async Task<int> UpsertExternalVariantPricesAsync(
            int productId,
            int siteId,
            IReadOnlyCollection<(int VariantId, decimal? Price, decimal? SalePrice)> items,
            CancellationToken cancelToken)
        {
            if (items == null || items.Count == 0) return 0;
            var existing = await _dbContext.ProductSiteVariantStock
                .Where(v => v.SiteId == siteId && v.ProductId == productId)
                .ToListAsync(cancelToken);
            var byVariant = new Dictionary<int, ProductSiteVariantStock>();
            foreach (var r in existing) byVariant[r.ProductVariantId] = r;
            var canonical = await _dbContext.ProductVariant.AsNoTracking()
                .Where(v => v.ProductId == productId && !v.IsDeleted)
                .Select(v => new { v.Id, v.Price, v.SalePrice })
                .ToDictionaryAsync(v => v.Id, cancelToken);

            var changed = 0;
            foreach (var (variantId, price, salePrice) in items)
            {
                byVariant.TryGetValue(variantId, out var row);
                if (row == null)
                {
                    // Nothing concrete to write - an all-null row just inherits the canonical variant.
                    if (!price.HasValue && !salePrice.HasValue) continue;
                    if (!canonical.TryGetValue(variantId, out var can)) continue;
                    var differs = (price.HasValue && can.Price != price) || can.SalePrice != salePrice;
                    if (!differs) continue;
                    row = new ProductSiteVariantStock
                    {
                        ProductVariantId = variantId,
                        SiteId = siteId,
                        ProductId = productId,
                        CreationTime = DateTime.UtcNow,
                    };
                    _dbContext.ProductSiteVariantStock.Add(row);
                    byVariant[variantId] = row;
                }

                var rowChanged = row.Id == 0;
                if (price.HasValue && row.Price != price) { row.Price = price; rowChanged = true; }
                if (row.SalePrice != salePrice) { row.SalePrice = salePrice; rowChanged = true; }
                if (rowChanged)
                {
                    row.UpdatedDate = DateTime.UtcNow;
                    changed++;
                }
            }

            if (changed > 0) await _dbContext.SaveChangesAsync(cancelToken);
            return changed;
        }

        /// <summary>Remove a per-site variation id mapping (used when a variation is deleted as an orphan on a site).</summary>
        public async Task DeleteSiteVariantWooIdAsync(int siteId, int wooVariationId, CancellationToken cancelToken)
        {
            var rows = await _dbContext.ProductSiteVariantWooId
                .Where(x => x.SiteId == siteId && x.WooCommerceVariationId == wooVariationId)
                .ToListAsync(cancelToken);
            if (rows.Count == 0) return;
            _dbContext.ProductSiteVariantWooId.RemoveRange(rows);
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
        /// <summary>
        /// Per-site merchandising override values for <see cref="UpsertOverrideAsync"/> (null = don't change).
        /// Status / Visibility / ShippingClass / Supplier are the client-side NAMES; they are resolved to lookup
        /// ids here (same normalization as ProductStorage.MapLookupsAsync; supplier is find-or-create per account).
        /// </summary>
        public sealed class MerchandisingOverrideUpsert
        {
            public decimal? CostPrice { get; set; }
            public bool? IsKosher { get; set; }
            public string? Status { get; set; }
            public string? Visibility { get; set; }
            public string? Slug { get; set; }
            public string? ShippingClass { get; set; }
            public string? Supplier { get; set; }
            public bool? LabelFrozen { get; set; }
            public bool? LabelGlutenFree { get; set; }
            public bool? LabelNotKosher { get; set; }
            public bool? LabelKosherForPassover { get; set; }
            public DateTime? LabelKosherForPassoverEndDate { get; set; }
            public bool? LabelNew { get; set; }
            public DateTime? LabelNewEndDate { get; set; }
            public bool? LabelBestseller { get; set; }
            public bool? LabelLowAvailability { get; set; }
            public bool? LabelReadyToCook { get; set; }
            public bool? LabelNatural { get; set; }
            public bool? LabelSugarFree { get; set; }
            public bool? LabelLactoseFree { get; set; }
        }

        /// <summary>Same client-name normalization as ProductStorage.MapLookupsAsync (status + visibility).</summary>
        private static string NormalizeStatusLikeName(string name)
        {
            if (name == "public" || name == "published") return "active";
            if (name == "archived") return "hidden";
            return name;
        }

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
            string? seoDescription = null,
            MerchandisingOverrideUpsert? merch = null)
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

                // Keep the per-site quantity consistent with an explicit status change when the caller didn't send a
                // quantity of its own (e.g. a status-only toggle from the list). A quantity-managed product derives
                // its Woo stock_status from quantity, so a stale per-site StockQuantity (e.g. a leftover 0) makes Woo
                // re-derive "out of stock" right after the site was toggled back in - the per-branch flip that only a
                // second toggle "fixed". Bug #5.
                if (!stockQuantity.HasValue)
                {
                    if (string.Equals(stockStatus, "out_of_stock", StringComparison.OrdinalIgnoreCase))
                        row.StockQuantity = 0;
                    else if (string.Equals(stockStatus, "in_stock", StringComparison.OrdinalIgnoreCase) && (row.StockQuantity ?? 0) <= 0)
                        row.StockQuantity = null; // drop the stale 0 and inherit the canonical quantity
                }
            }
            if (stockQuantity.HasValue) row.StockQuantity = stockQuantity;
            if (variationStockByQuantity.HasValue) row.VariationStockByQuantity = variationStockByQuantity;
            if (lowStockThreshold.HasValue) row.LowStockThreshold = lowStockThreshold.Value <= 0 ? (decimal?)null : lowStockThreshold.Value;

            if (merch != null)
            {
                if (merch.CostPrice.HasValue) row.CostPrice = merch.CostPrice;
                if (merch.IsKosher.HasValue) row.IsKosher = merch.IsKosher;
                if (merch.Slug != null) row.Slug = string.IsNullOrWhiteSpace(merch.Slug) ? null : merch.Slug.Trim();

                if (merch.Status.HasValue())
                {
                    var statusName = NormalizeStatusLikeName(merch.Status!.Trim());
                    var status = await _dbContext.ProductStatus
                        .FirstOrDefaultAsync(s => s.Name.ToLower() == statusName.ToLower(), cancelToken);
                    if (status != null) row.StatusId = status.Id; // don't null out on an unresolvable name
                }
                if (merch.Visibility.HasValue())
                {
                    var visibilityName = NormalizeStatusLikeName(merch.Visibility!.Trim());
                    var visibility = await _dbContext.Visibility
                        .FirstOrDefaultAsync(v => v.Name.ToLower() == visibilityName.ToLower(), cancelToken);
                    if (visibility != null) row.VisibilityId = visibility.Id;
                }
                if (merch.ShippingClass.HasValue())
                {
                    var sc = await _dbContext.ShippingClass
                        .FirstOrDefaultAsync(s => s.Name == merch.ShippingClass, cancelToken);
                    if (sc != null) row.ShippingClassId = sc.Id;
                }
                if (merch.Supplier != null)
                {
                    // Empty string clears the per-site supplier back to inheriting; a name is find-or-create per account
                    // (same as the canonical MapLookupsAsync supplier handling).
                    var supplierName = merch.Supplier.Trim();
                    if (supplierName.Length == 0)
                    {
                        row.SupplierId = null;
                    }
                    else
                    {
                        var supplier = await _dbContext.Supplier
                            .FirstOrDefaultAsync(s => s.Name == supplierName && s.AccountId == accountId && !s.IsDeleted, cancelToken);
                        if (supplier == null)
                        {
                            supplier = new Supplier
                            {
                                Name = supplierName,
                                AccountId = accountId,
                                IsDeleted = false,
                                CreationTime = DateTime.UtcNow,
                            };
                            _dbContext.Supplier.Add(supplier);
                            await _dbContext.SaveChangesAsync(cancelToken);
                        }
                        row.SupplierId = supplier.Id;
                    }
                }

                if (merch.LabelFrozen.HasValue) row.LabelFrozen = merch.LabelFrozen;
                if (merch.LabelGlutenFree.HasValue) row.LabelGlutenFree = merch.LabelGlutenFree;
                if (merch.LabelNotKosher.HasValue) row.LabelNotKosher = merch.LabelNotKosher;
                if (merch.LabelKosherForPassover.HasValue)
                {
                    row.LabelKosherForPassover = merch.LabelKosherForPassover;
                    row.LabelKosherForPassoverEndDate = merch.LabelKosherForPassover.Value ? merch.LabelKosherForPassoverEndDate : null;
                }
                if (merch.LabelNew.HasValue)
                {
                    row.LabelNew = merch.LabelNew;
                    row.LabelNewEndDate = merch.LabelNew.Value ? merch.LabelNewEndDate : null;
                }
                if (merch.LabelBestseller.HasValue) row.LabelBestseller = merch.LabelBestseller;
                if (merch.LabelLowAvailability.HasValue) row.LabelLowAvailability = merch.LabelLowAvailability;
                if (merch.LabelReadyToCook.HasValue) row.LabelReadyToCook = merch.LabelReadyToCook;
                if (merch.LabelNatural.HasValue) row.LabelNatural = merch.LabelNatural;
                if (merch.LabelSugarFree.HasValue) row.LabelSugarFree = merch.LabelSugarFree;
                if (merch.LabelLactoseFree.HasValue) row.LabelLactoseFree = merch.LabelLactoseFree;
            }

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
            if (All("sku")) { row.Sku = null; }
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

        /// <summary>
        /// Removes every per-site override VALUE artifact for (product, site): the override row, the per-site
        /// variant rows, and - only when the canonical product has its own rows to fall back to - the per-site
        /// image and category rows. Used when a single-site product of a non-network account is written
        /// canonically: the canonical row is now the source of truth, and a stale override left behind would
        /// shadow the fresh canonical values on read and in the Woo sync. A variant excluded on the product's
        /// ONLY site is effectively deleted, so it is soft-deleted canonically before its row is dropped.
        /// Per-site Woo-ID mappings (ProductSiteWooId / ProductSiteVariantWooId) are intentionally untouched.
        /// </summary>
        public async Task DeleteSiteOverrideArtifactsAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            var overrideRow = await _dbContext.ProductSiteOverride
                .FirstOrDefaultAsync(o => o.ProductId == productId && o.SiteId == siteId, cancelToken);
            if (overrideRow != null)
                _dbContext.ProductSiteOverride.Remove(overrideRow);

            var variantRows = await _dbContext.ProductSiteVariantStock
                .Include(v => v.ProductVariant)
                .Where(v => v.SiteId == siteId && (v.ProductId == productId || v.ProductVariant.ProductId == productId))
                .ToListAsync(cancelToken);
            foreach (var vr in variantRows)
            {
                if (vr.IsExcluded && vr.ProductVariant != null)
                    vr.ProductVariant.IsDeleted = true;
                _dbContext.ProductSiteVariantStock.Remove(vr);
            }

            if (await _dbContext.ProductImage.AnyAsync(pi => pi.ProductId == productId, cancelToken))
            {
                var imageRows = await _dbContext.ProductSiteImage
                    .Where(i => i.ProductId == productId && i.SiteId == siteId)
                    .ToListAsync(cancelToken);
                _dbContext.ProductSiteImage.RemoveRange(imageRows);
            }

            if (await _dbContext.ProductCategory.AnyAsync(pc => pc.ProductId == productId, cancelToken))
            {
                var categoryRows = await _dbContext.ProductSiteCategory
                    .Where(c => c.ProductId == productId && c.SiteId == siteId)
                    .ToListAsync(cancelToken);
                _dbContext.ProductSiteCategory.RemoveRange(categoryRows);
            }

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
                    // Per-site merchandising overrides (canonical + per-site override, like Price).
                    if (row.CostPrice == null) row.CostPrice = product.CostPrice;
                    if (row.IsKosher == null) row.IsKosher = product.IsKosher;
                    if (row.StatusId == null) row.StatusId = product.StatusId;
                    if (row.VisibilityId == null) row.VisibilityId = product.VisibilityId;
                    if (row.Slug == null) row.Slug = product.Slug;
                    if (row.ShippingClassId == null) row.ShippingClassId = product.ShippingClassId;
                    if (row.SupplierId == null) row.SupplierId = product.SupplierId;
                    if (row.LabelFrozen == null) row.LabelFrozen = product.LabelFrozen;
                    if (row.LabelGlutenFree == null) row.LabelGlutenFree = product.LabelGlutenFree;
                    if (row.LabelNotKosher == null) row.LabelNotKosher = product.LabelNotKosher;
                    if (row.LabelKosherForPassover == null)
                    {
                        row.LabelKosherForPassover = product.LabelKosherForPassover;
                        row.LabelKosherForPassoverEndDate = product.LabelKosherForPassoverEndDate;
                    }
                    if (row.LabelNew == null)
                    {
                        row.LabelNew = product.LabelNew;
                        row.LabelNewEndDate = product.LabelNewEndDate;
                    }
                    if (row.LabelBestseller == null) row.LabelBestseller = product.LabelBestseller;
                    if (row.LabelLowAvailability == null) row.LabelLowAvailability = product.LabelLowAvailability;
                    if (row.LabelReadyToCook == null) row.LabelReadyToCook = product.LabelReadyToCook;
                    if (row.LabelNatural == null) row.LabelNatural = product.LabelNatural;
                    if (row.LabelSugarFree == null) row.LabelSugarFree = product.LabelSugarFree;
                    if (row.LabelLactoseFree == null) row.LabelLactoseFree = product.LabelLactoseFree;
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

            // Excluded overrides - filter by the product's account (robust even if the override's
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
