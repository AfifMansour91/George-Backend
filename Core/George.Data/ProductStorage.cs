using George.Common;
using George.Common.Request;
using George.Data.Dto;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class ProductStorage : StorageBase
    {
        public ProductStorage(GeorgeDBContext dbContext, ILogger<ProductStorage> logger)
            : base(dbContext, logger)
        {
        }

        /// <summary>
        /// ProductOptionValue is keyed by (ProductOptionId, Value); duplicate strings in the request must not create multiple rows.
        /// </summary>
        private static List<string> DistinctOptionValuesPreserveOrder(List<string>? values)
        {
            if (values == null || values.Count == 0)
                return new List<string>();

            var seen = new HashSet<string>();
            var result = new List<string>();
            foreach (var v in values)
            {
                if (string.IsNullOrWhiteSpace(v))
                    continue;
                if (seen.Add(v))
                    result.Add(v);
            }

            return result;
        }

        private static List<int> DistinctPositiveIdsPreserveOrder(IEnumerable<int> ids)
        {
            var seen = new HashSet<int>();
            var result = new List<int>();
            foreach (var id in ids)
            {
                if (id <= 0 || !seen.Add(id)) continue;
                result.Add(id);
            }
            return result;
        }

        public async Task<DataListResult<Product>> GetProductsAsync(
            ProductFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Product>();

            // Lighter includes for list; ProductOption and ProductVariant only when requested (e.g. My Products page needs them for "with variations" filter)
            var query = _dbContext.Product
                .Include(p => p.Brand)
                .Include(p => p.ProductBrand)
                    .ThenInclude(pb => pb.Brand)
                .Include(p => p.Supplier)
                .Include(p => p.Status)
                .Include(p => p.Visibility)
                .Include(p => p.StockManagementType)
                .Include(p => p.StockStatus)
                .Include(p => p.ShippingClass)
                .Include(p => p.SetupType)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc!.Unit)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc!.UnitWeightMode)
                .Include(p => p.Site)
                .Include(p => p.Tag)
                .Include(p => p.ProductCategory)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImage)
                    .ThenInclude(pi => pi.Media)
                .Include(p => p.ProductVariant)
                    .ThenInclude(pv => pv.ProductVariantOptionValue)
                .AsNoTracking()
                .Where(p => !p.IsDeleted);

            if (filter?.IncludeOptionsAndVariants == true)
            {
                query = query
                    .Include(p => p.ProductOption)
                        .ThenInclude(po => po.ProductOptionValue)
                    .Include(p => p.ProductVariant)
                        .ThenInclude(pv => pv.ProductVariantOptionValue);
            }

            // Related/complementary are not loaded here; kiosk gets them via GetUpsellProductIdsForSite when needed.

            // Apply filters
            if (filter != null)
            {
                if (filter.AccountId.HasValue)
                {
                    query = query.Where(p => p.AccountId == filter.AccountId.Value);
                }

                if (filter.SiteId.HasValue)
                {
                    query = query.Where(p => p.Site.Any(s => s.Id == filter.SiteId.Value));
                }

                if (filter.CategoryId.HasValue)
                {
                    query = query.Where(p => p.ProductCategory.Any(pc => pc.CategoryId == filter.CategoryId.Value));
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(p => p.Name.Contains(term) ||
                                           (p.Sku != null && p.Sku.Contains(term)) ||
                                           p.ProductVariant.Any(v => !v.IsDeleted && v.Sku != null && v.Sku.Contains(term)) ||
                                           (p.ShortDescription != null && p.ShortDescription.Contains(term)));
                }

                if (filter.Status.HasValue())
                {
                    query = query.Where(p => p.Status != null && p.Status.Name == filter.Status);
                }
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query
                .OrderBy(p => p.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(p => p.CreationTime);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);
            return res;
        }

        /// <summary>Unions RelatedProduct (Woo up-sells) and ComplementaryProduct (Woo cross-sell) IDs for the given cart lines, filtered to products on the site. For kiosk POS linked-products step.</summary>
        public async Task<List<int>> GetUpsellProductIdsForSiteAsync(int siteId, List<int> productIds, CancellationToken cancelToken)
        {
            if (siteId <= 0 || productIds == null || productIds.Count == 0)
                return new List<int>();

            var relatedIds = await _dbContext.Product
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .SelectMany(p => p.RelatedProduct.Select(r => r.Id))
                .Distinct()
                .ToListAsync(cancelToken).ConfigureAwait(false);

            var complementaryIds = await _dbContext.Product
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .SelectMany(p => p.ComplementaryProduct.Select(c => c.Id))
                .Distinct()
                .ToListAsync(cancelToken).ConfigureAwait(false);

            var allUpsellIds = relatedIds.Union(complementaryIds).ToHashSet();
            if (allUpsellIds.Count == 0)
                return new List<int>();

            var inSite = await _dbContext.Product
                .AsNoTracking()
                .Where(p => allUpsellIds.Contains(p.Id) && !p.IsDeleted && p.Site.Any(s => s.Id == siteId))
                .Select(p => p.Id)
                .ToListAsync(cancelToken).ConfigureAwait(false);
            return inSite;
        }

        /// <summary>Get product IDs for a site (lightweight, no includes). Used when syncing all products so each can be loaded with GetProductAsync for full options/variants/weight.</summary>
        public async Task<List<int>> GetProductIdsForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            if (siteId <= 0) return new List<int>();
            return await _dbContext.Product
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.Site.Any(s => s.Id == siteId))
                .OrderBy(p => p.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(p => p.CreationTime)
                .Select(p => p.Id)
                .ToListAsync(cancelToken).ConfigureAwait(false);
        }

        /// <summary>Get products by site and a list of product IDs (e.g. for kiosk past purchases). Same includes as GetProductsAsync. Excludes deleted.</summary>
        public async Task<DataListResult<Product>> GetProductsBySiteAndIdsAsync(
            int siteId,
            List<int> productIds,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Product>();
            if (siteId <= 0 || productIds == null || productIds.Count == 0)
                return res;

            var query = _dbContext.Product
                .Include(p => p.Brand)
                .Include(p => p.ProductBrand)
                    .ThenInclude(pb => pb.Brand)
                .Include(p => p.Supplier)
                .Include(p => p.Status)
                .Include(p => p.Visibility)
                .Include(p => p.StockManagementType)
                .Include(p => p.StockStatus)
                .Include(p => p.ShippingClass)
                .Include(p => p.SetupType)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc!.Unit)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc!.UnitWeightMode)
                .Include(p => p.Site)
                .Include(p => p.Tag)
                .Include(p => p.ProductCategory)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImage)
                    .ThenInclude(pi => pi.Media)
                .Include(p => p.ProductVariant)
                    .ThenInclude(pv => pv.ProductVariantOptionValue)
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.Site.Any(s => s.Id == siteId) && productIds.Contains(p.Id));

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            res.Items = await query
                .OrderBy(p => p.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(p => p.CreationTime)
                .Skip(paging.Skip)
                .Take(paging.Take)
                .ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Product?> GetProductAsync(int productId, CancellationToken cancelToken)
        {
            return await _dbContext.Product
                .Include(p => p.Brand)
                .Include(p => p.Supplier)
                .Include(p => p.Status)
                .Include(p => p.Visibility)
                .Include(p => p.StockManagementType)
                .Include(p => p.StockStatus)
                .Include(p => p.ShippingClass)
                .Include(p => p.SetupType)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc.Unit)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc.UnitWeightMode)
                .Include(p => p.Site)
                .Include(p => p.Tag)
                .Include(p => p.ProductCategory)
                    .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductBrand)
                    .ThenInclude(pb => pb.Brand)
                .Include(p => p.ProductImage)
                    .ThenInclude(pi => pi.Media)
                .Include(p => p.ProductOption)
                    .ThenInclude(po => po.ProductOptionValue)
                .Include(p => p.ProductVariant)
                    .ThenInclude(pv => pv.ProductVariantOptionValue)
                .Include(p => p.RelatedProduct)
                .Include(p => p.ComplementaryProduct)
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);
        }

        public async Task<Product> CreateProductAsync(Product product, List<int>? siteIds, List<int>? categoryIds, List<int>? brandIds, List<string>? tags, List<int>? relatedProductIds, List<int>? complementaryProductIds, CancellationToken cancelToken)
        {
            // Normalize empty SKU to NULL to avoid unique constraint violations
            if (string.IsNullOrWhiteSpace(product.Sku))
            {
                product.Sku = null;
            }
            // Sort: DisplayOrder ascending, then CreationTime desc. Client may set DisplayOrder (e.g. wizard order).
            if (!product.DisplayOrder.HasValue)
                product.DisplayOrder = 0;
            _dbContext.Product.Add(product);

            // Add sites
            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Site
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                foreach (var site in sites)
                {
                    product.Site.Add(site);
                }
            }

            // Add categories
            if (categoryIds != null && categoryIds.Any())
            {
                var categories = await _dbContext.Category
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync(cancelToken);
                foreach (var category in categories)
                {
                    product.ProductCategory.Add(new ProductCategory
                    {
                        ProductId = product.Id,
                        CategoryId = category.Id
                    });
                }
            }

            // Brands (many-to-many). When brandIds is null, fall back to legacy Product.BrandId from MapLookups.
            if (brandIds != null)
            {
                var ordered = DistinctPositiveIdsPreserveOrder(brandIds);
                if (ordered.Count > 0)
                {
                    var accountId = product.AccountId;
                    var valid = await _dbContext.Brand
                        .Where(b => ordered.Contains(b.Id) && !b.IsDeleted && (!accountId.HasValue || b.AccountId == accountId))
                        .Select(b => b.Id)
                        .ToListAsync(cancelToken)
                        .ConfigureAwait(false);
                    var validSet = valid.ToHashSet();
                    var hasPrimary = false;
                    foreach (var bid in ordered)
                    {
                        if (!validSet.Contains(bid)) continue;
                        product.ProductBrand.Add(new ProductBrand
                        {
                            BrandId = bid,
                            IsPrimary = !hasPrimary,
                        });
                        hasPrimary = true;
                    }
                }
            }
            else if (product.BrandId.HasValue)
            {
                product.ProductBrand.Add(new ProductBrand
                {
                    BrandId = product.BrandId.Value,
                    IsPrimary = true,
                });
            }

            // Add tags
            if (tags != null && tags.Any())
            {
                foreach (var tagName in tags)
                {
                    var tag = await _dbContext.Tag
                        .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == product.AccountId, cancelToken);
                    
                    if (tag == null)
                    {
                        tag = new Tag
                        {
                            Name = tagName,
                            AccountId = product.AccountId,
                            CreationTime = DateTime.UtcNow
                        };
                        _dbContext.Tag.Add(tag);
                    }
                    product.Tag.Add(tag);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);

            // Add related (נלווים) and complementary (מוצרים משלימים) products after we have product.Id
            if ((relatedProductIds != null && relatedProductIds.Any()) || (complementaryProductIds != null && complementaryProductIds.Any()))
            {
                var dbProduct = await _dbContext.Product
                    .Include(p => p.RelatedProduct)
                    .Include(p => p.ComplementaryProduct)
                    .FirstAsync(p => p.Id == product.Id, cancelToken);
                if (relatedProductIds != null)
                {
                    foreach (var id in relatedProductIds.Where(id => id != product.Id))
                    {
                        var related = await _dbContext.Product.FindAsync(new object[] { id }, cancelToken);
                        if (related != null && !related.IsDeleted && related.AccountId == product.AccountId)
                            dbProduct.RelatedProduct.Add(related);
                    }
                }
                if (complementaryProductIds != null)
                {
                    foreach (var id in complementaryProductIds.Where(id => id != product.Id))
                    {
                        var comp = await _dbContext.Product.FindAsync(new object[] { id }, cancelToken);
                        if (comp != null && !comp.IsDeleted && comp.AccountId == product.AccountId)
                            dbProduct.ComplementaryProduct.Add(comp);
                    }
                }
                await _dbContext.SaveChangesAsync(cancelToken);
                product = await GetProductAsync(product.Id, cancelToken) ?? product;
            }

            return product;
        }

        public async Task<Product?> UpdateProductAsync(Product updated, List<int>? siteIds, List<int>? categoryIds, List<int>? brandIds, List<string>? tags, List<int>? relatedProductIds, List<int>? complementaryProductIds, CancellationToken cancelToken)
        {
            var dbProduct = await _dbContext.Product
                .Include(p => p.Site)
                .Include(p => p.Tag)
                .Include(p => p.ProductCategory)
                .Include(p => p.ProductBrand)
                .Include(p => p.RelatedProduct)
                .Include(p => p.ComplementaryProduct)
                .FirstOrDefaultAsync(p => p.Id == updated.Id, cancelToken);

            if (dbProduct == null) return null;

            // Update basic properties
            dbProduct.Name = updated.Name;
            dbProduct.ShortDescription = updated.ShortDescription;
            dbProduct.LongDescription = updated.LongDescription;
            dbProduct.Price = updated.Price;
            dbProduct.SalePrice = updated.SalePrice;
            dbProduct.SalePriceStartDate = updated.SalePriceStartDate;
            dbProduct.SalePriceEndDate = updated.SalePriceEndDate;
            dbProduct.CostPrice = updated.CostPrice;
            // Normalize empty SKU to NULL to avoid unique constraint violations
            dbProduct.Sku = string.IsNullOrWhiteSpace(updated.Sku) ? null : updated.Sku;
            dbProduct.StockQuantity = updated.StockQuantity;
            dbProduct.Weight = updated.Weight;
            dbProduct.IsKosher = updated.IsKosher;
            dbProduct.IsWeighted = updated.IsWeighted;
            dbProduct.ShowAsMl = updated.ShowAsMl;
            dbProduct.WeightUnit = updated.WeightUnit;
            dbProduct.BrandId = updated.BrandId;
            dbProduct.SupplierId = updated.SupplierId;
            dbProduct.StatusId = updated.StatusId;
            dbProduct.VisibilityId = updated.VisibilityId;
            dbProduct.StockManagementTypeId = updated.StockManagementTypeId;
            dbProduct.StockStatusId = updated.StockStatusId;
            dbProduct.VariationStockByQuantity = updated.VariationStockByQuantity;
            dbProduct.ShippingClassId = updated.ShippingClassId;
            dbProduct.SetupTypeId = updated.SetupTypeId;
            dbProduct.WeightConfigId = updated.WeightConfigId;
            dbProduct.SeoTitle = updated.SeoTitle;
            dbProduct.SeoDescription = updated.SeoDescription;
            dbProduct.Slug = string.IsNullOrWhiteSpace(updated.Slug) ? null : updated.Slug.Trim();
            dbProduct.LowStockThreshold = updated.LowStockThreshold;
            dbProduct.LabelFrozen = updated.LabelFrozen;
            dbProduct.LabelGlutenFree = updated.LabelGlutenFree;
            dbProduct.LabelNotKosher = updated.LabelNotKosher;
            dbProduct.LabelBestseller = updated.LabelBestseller;
            dbProduct.LabelLowAvailability = updated.LabelLowAvailability;
            dbProduct.LabelReadyToCook = updated.LabelReadyToCook;
            dbProduct.LabelNatural = updated.LabelNatural;
            dbProduct.LabelSugarFree = updated.LabelSugarFree;
            dbProduct.LabelLactoseFree = updated.LabelLactoseFree;
            dbProduct.LabelKosherForPassover = updated.LabelKosherForPassover;
            dbProduct.LabelKosherForPassoverEndDate = updated.LabelKosherForPassoverEndDate;
            dbProduct.LabelNew = updated.LabelNew;
            dbProduct.LabelNewEndDate = updated.LabelNewEndDate;
            if (updated.DisplayOrder.HasValue)
                dbProduct.DisplayOrder = updated.DisplayOrder;
            dbProduct.UpdatedDate = DateTime.UtcNow;
            dbProduct.UpdateUserId = updated.UpdateUserId;

            // Update sites only when the caller sends one or more site IDs.
            // Null or empty list means "leave site assignment unchanged" (partial updates and clients that omit site_ids).
            if (siteIds != null && siteIds.Any())
            {
                dbProduct.Site.Clear();
                var sites = await _dbContext.Site
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                foreach (var site in sites)
                {
                    dbProduct.Site.Add(site);
                }
            }

            // Update categories
            if (categoryIds != null)
            {
                dbProduct.ProductCategory.Clear();
                if (categoryIds.Any())
                {
                    var categories = await _dbContext.Category
                        .Where(c => categoryIds.Contains(c.Id))
                        .ToListAsync(cancelToken);
                    foreach (var category in categories)
                    {
                        dbProduct.ProductCategory.Add(new ProductCategory
                        {
                            ProductId = dbProduct.Id,
                            CategoryId = category.Id
                        });
                    }
                }
            }

            // Update brands (many-to-many). Null brandIds = leave unchanged (partial updates).
            if (brandIds != null)
            {
                dbProduct.ProductBrand.Clear();
                var ordered = DistinctPositiveIdsPreserveOrder(brandIds);
                if (ordered.Count > 0)
                {
                    var accountId = dbProduct.AccountId;
                    var valid = await _dbContext.Brand
                        .Where(b => ordered.Contains(b.Id) && !b.IsDeleted && (!accountId.HasValue || b.AccountId == accountId))
                        .Select(b => b.Id)
                        .ToListAsync(cancelToken)
                        .ConfigureAwait(false);
                    var validSet = valid.ToHashSet();
                    var hasPrimary = false;
                    foreach (var bid in ordered)
                    {
                        if (!validSet.Contains(bid)) continue;
                        dbProduct.ProductBrand.Add(new ProductBrand
                        {
                            ProductId = dbProduct.Id,
                            BrandId = bid,
                            IsPrimary = !hasPrimary,
                        });
                        hasPrimary = true;
                    }
                }
            }

            // Update tags
            if (tags != null)
            {
                dbProduct.Tag.Clear();
                if (tags.Any())
                {
                    foreach (var tagName in tags)
                    {
                        var tag = await _dbContext.Tag
                            .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == dbProduct.AccountId, cancelToken);
                        
                        if (tag == null)
                        {
                            tag = new Tag
                            {
                                Name = tagName,
                                AccountId = dbProduct.AccountId,
                                CreationTime = DateTime.UtcNow
                            };
                            _dbContext.Tag.Add(tag);
                        }
                        dbProduct.Tag.Add(tag);
                    }
                }
            }

            // Update related products (נלווים)
            if (relatedProductIds != null)
            {
                dbProduct.RelatedProduct.Clear();
                foreach (var id in relatedProductIds.Where(id => id != dbProduct.Id))
                {
                    var related = await _dbContext.Product.FindAsync(new object[] { id }, cancelToken);
                    if (related != null && !related.IsDeleted && related.AccountId == dbProduct.AccountId)
                        dbProduct.RelatedProduct.Add(related);
                }
            }

            // Update complementary products (מוצרים משלימים)
            if (complementaryProductIds != null)
            {
                dbProduct.ComplementaryProduct.Clear();
                foreach (var id in complementaryProductIds.Where(id => id != dbProduct.Id))
                {
                    var comp = await _dbContext.Product.FindAsync(new object[] { id }, cancelToken);
                    if (comp != null && !comp.IsDeleted && comp.AccountId == dbProduct.AccountId)
                        dbProduct.ComplementaryProduct.Add(comp);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbProduct;
        }

        /// <summary>
        /// Removes the product from the given site only (unlinks ProductSite). Other sites keep the product.
        /// If this was the last site linked to the product, soft-deletes the product.
        /// </summary>
        public async Task<bool> RemoveProductFromSiteAsync(int productId, int siteId, CancellationToken cancelToken)
        {
            var product = await _dbContext.Product
                .Include(p => p.Site)
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);

            if (product == null) return false;

            var siteToRemove = product.Site.FirstOrDefault(s => s.Id == siteId);
            if (siteToRemove == null) return true; // already not on this site

            product.Site.Remove(siteToRemove);
            product.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);

            if (!product.Site.Any())
            {
                product.IsDeleted = true;
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return true;
        }

        public async Task<bool> DeleteProductAsync(int productId, CancellationToken cancelToken)
        {
            var product = await _dbContext.Product
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);

            if (product == null) return false;

            product.IsDeleted = true;
            product.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>Product IDs from the given set, sorted the same way as the product list API (DisplayOrder asc, CreationTime desc).</summary>
        public async Task<List<int>> GetProductIdsInDisplayOrderAsync(List<int> productIds, CancellationToken cancelToken)
        {
            if (productIds == null || !productIds.Any()) return new List<int>();
            var idSet = productIds.ToHashSet();
            return await _dbContext.Product
                .AsNoTracking()
                .Where(p => idSet.Contains(p.Id))
                .OrderBy(p => p.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(p => p.CreationTime)
                .Select(p => p.Id)
                .ToListAsync(cancelToken);
        }

        public async Task UpdateProductOrderAsync(List<int> productIds, CancellationToken cancelToken)
        {
            if (productIds == null || !productIds.Any()) return;
            var products = await _dbContext.Product
                .Where(p => productIds.Contains(p.Id))
                .ToListAsync(cancelToken);
            var idToIndex = productIds.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
            var now = DateTime.UtcNow;
            foreach (var product in products)
            {
                if (idToIndex.TryGetValue(product.Id, out int order))
                {
                    product.DisplayOrder = order;
                    product.UpdatedDate = now;
                }
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>Lightweight product rows for order-debug preview (sorted like the product list API).</summary>
        public async Task<List<(int Id, string Name, int? DisplayOrder, int? WooCommerceId, string? Sku)>> GetProductOrderPreviewRowsForSiteAsync(
            int siteId,
            CancellationToken cancelToken)
        {
            if (siteId <= 0) return new List<(int, string, int?, int?, string?)>();
            var rows = await _dbContext.Product
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.Site.Any(s => s.Id == siteId))
                .OrderBy(p => p.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(p => p.CreationTime)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.DisplayOrder,
                    WooCommerceId = p.WooCommerceId,
                    p.Sku
                })
                .ToListAsync(cancelToken);
            return rows
                .Select(r => (r.Id, r.Name, r.DisplayOrder, r.WooCommerceId, r.Sku))
                .ToList();
        }

        /// <summary>WooCommerce REST product id → local Product.Id for products on the site that have a WooCommerceId.</summary>
        public async Task<Dictionary<int, int>> GetWooCommerceIdToProductIdForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            if (siteId <= 0) return new Dictionary<int, int>();
            var rows = await _dbContext.Product
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.WooCommerceId != null && p.WooCommerceId > 0 && p.Site.Any(s => s.Id == siteId))
                .Select(p => new { p.Id, WooId = p.WooCommerceId!.Value })
                .ToListAsync(cancelToken);
            return rows.ToDictionary(r => r.WooId, r => r.Id);
        }

        /// <summary>Batch-update DisplayOrder by product id. Returns number of rows updated.</summary>
        public async Task<int> UpdateDisplayOrdersForProductsAsync(Dictionary<int, int> productIdToDisplayOrder, CancellationToken cancelToken)
        {
            if (productIdToDisplayOrder == null || productIdToDisplayOrder.Count == 0) return 0;
            var ids = productIdToDisplayOrder.Keys.ToList();
            var products = await _dbContext.Product
                .Where(p => ids.Contains(p.Id))
                .ToListAsync(cancelToken);
            var now = DateTime.UtcNow;
            var updated = 0;
            foreach (var product in products)
            {
                if (productIdToDisplayOrder.TryGetValue(product.Id, out var order) && product.DisplayOrder != order)
                {
                    product.DisplayOrder = order;
                    product.UpdatedDate = now;
                    updated++;
                }
            }
            if (updated > 0)
                await _dbContext.SaveChangesAsync(cancelToken);
            return updated;
        }

        /// <summary>Returns distinct category IDs assigned to the given products on a site.</summary>
        public async Task<List<int>> GetCategoryIdsForProductsOnSiteAsync(List<int> productIds, int siteId, CancellationToken cancelToken)
        {
            if (productIds == null || !productIds.Any() || siteId <= 0)
                return new List<int>();
            var idSet = productIds.Distinct().ToList();
            return await _dbContext.Product
                .AsNoTracking()
                .Where(p => idSet.Contains(p.Id) && !p.IsDeleted && p.Site.Any(s => s.Id == siteId))
                .SelectMany(p => p.ProductCategory.Select(pc => pc.CategoryId))
                .Distinct()
                .ToListAsync(cancelToken);
        }

        /// <summary>Returns siteId -> list of product IDs for products that belong to that site. Used to sync order to WooCommerce per site.</summary>
        public async Task<Dictionary<int, List<int>>> GetProductIdsBySiteForProductIdsAsync(List<int> productIds, CancellationToken cancelToken)
        {
            if (productIds == null || !productIds.Any()) return new Dictionary<int, List<int>>();
            var pairs = await _dbContext.Product
                .AsNoTracking()
                .Where(p => productIds.Contains(p.Id))
                .SelectMany(p => p.Site.Select(s => new { ProductId = p.Id, SiteId = s.Id }))
                .ToListAsync(cancelToken);
            return pairs.GroupBy(x => x.SiteId).ToDictionary(g => g.Key, g => g.Select(x => x.ProductId).ToList());
        }

        /// <summary>Resolves WooCommerce product ID to our Product.Id for the given site. Used when receiving orders from WooCommerce so order items link to the correct site product. Returns null if no product is found for that site with the given WooCommerceId.</summary>
        public async Task<int?> GetProductIdByWooCommerceIdAndSiteAsync(int siteId, int wooCommerceProductId, CancellationToken cancelToken = default)
        {
            if (siteId <= 0) return null;
            var product = await _dbContext.Product
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.WooCommerceId == wooCommerceProductId && p.Site.Any(s => s.Id == siteId))
                .Select(p => new { p.Id })
                .FirstOrDefaultAsync(cancelToken);
            return product?.Id;
        }

        /// <summary>
        /// When Woo sends a <b>variation</b> id as <c>productId</c> (or in <c>variationId</c>), parent <see cref="Product.WooCommerceId"/> lookup fails.
        /// Resolve to our <see cref="Product.Id"/> via <see cref="ProductVariant.WooCommerceVariationId"/> so order lines link and display snapshots can run.
        /// </summary>
        public async Task<int?> GetProductIdByWooCommerceVariationIdAndSiteAsync(int siteId, int wooVariationId, CancellationToken cancelToken = default)
        {
            if (siteId <= 0 || wooVariationId <= 0) return null;
            var productId = await _dbContext.ProductVariant
                .AsNoTracking()
                .Where(v => !v.IsDeleted && v.WooCommerceVariationId == wooVariationId)
                .Where(v => !v.Product.IsDeleted && v.Product.Site.Any(s => s.Id == siteId))
                .Select(v => (int?)v.ProductId)
                .FirstOrDefaultAsync(cancelToken);
            return productId;
        }

        /// <summary>Returns (WooCommerceId, DisplayOrder) for products in orderedProductIds that belong to the site and have a WooCommerceId. DisplayOrder = index in orderedProductIds. Used for menu-order-only sync.</summary>
        public async Task<List<(int WooCommerceId, int DisplayOrder)>> GetWooCommerceIdAndDisplayOrderForSiteAsync(
            List<int> orderedProductIds,
            int siteId,
            CancellationToken cancelToken,
            IReadOnlySet<int>? onlyProductIds = null)
        {
            if (orderedProductIds == null || !orderedProductIds.Any()) return new List<(int, int)>();
            var productIdsSet = orderedProductIds.Distinct().ToHashSet();
            var products = await _dbContext.Product
                .AsNoTracking()
                .Where(p => productIdsSet.Contains(p.Id) && p.WooCommerceId != null && p.WooCommerceId > 0 && p.Site.Any(s => s.Id == siteId))
                .Select(p => new { p.Id, WooId = p.WooCommerceId!.Value })
                .ToListAsync(cancelToken);
            var idToWooId = products.ToDictionary(p => p.Id, p => p.WooId);
            var result = new List<(int, int)>();
            for (var i = 0; i < orderedProductIds.Count; i++)
            {
                var productId = orderedProductIds[i];
                if (onlyProductIds != null && !onlyProductIds.Contains(productId))
                    continue;
                if (idToWooId.TryGetValue(productId, out var wooId))
                    result.Add((wooId, i));
            }
            return result;
        }

        public async Task<bool> UpdateProductWooCommerceIdAsync(int productId, int? wooCommerceId, CancellationToken cancelToken)
        {
            var product = await _dbContext.Product
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);

            if (product == null) return false;

            product.WooCommerceId = wooCommerceId;
            product.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> UpdateProductVariantWooCommerceIdAsync(int variantId, int? wooCommerceVariationId, CancellationToken cancelToken)
        {
            var variant = await _dbContext.ProductVariant
                .FirstOrDefaultAsync(pv => pv.Id == variantId, cancelToken);

            if (variant == null) return false;

            variant.WooCommerceVariationId = wooCommerceVariationId;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        // Helper methods for service layer. Each item has Url and optional MediaId (when linked to account media).
        public async Task CreateProductImagesAsync(int productId, List<(string Url, int? MediaId)> images, CancellationToken cancelToken)
        {
            var existingImages = await _dbContext.ProductImage
                .Where(pi => pi.ProductId == productId)
                .ToListAsync(cancelToken);

            _dbContext.ProductImage.RemoveRange(existingImages);

            for (int i = 0; i < images.Count; i++)
            {
                var (url, mediaId) = images[i];
                _dbContext.ProductImage.Add(new ProductImage
                {
                    ProductId = productId,
                    Url = url,
                    MediaId = mediaId,
                    SortOrder = i
                });
            }

            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <param name="limitAttributeToSiteIds">When set, create attributes only for these site IDs (e.g. import target site). When null, create for all sites the product is on.</param>
        public async Task CreateProductOptionsAsync(int productId, List<ProductOptionDto> options, List<int>? limitAttributeToSiteIds = null, CancellationToken cancelToken = default)
        {
            // Get product's sites to create attributes for each site (or only for limitAttributeToSiteIds when provided)
            var product = await _dbContext.Product
                .Include(p => p.Site)
                .FirstOrDefaultAsync(p => p.Id == productId, cancelToken);
            
            var siteIds = limitAttributeToSiteIds != null && limitAttributeToSiteIds.Any()
                ? limitAttributeToSiteIds
                : (product?.Site?.Select(s => s.Id).ToList() ?? new List<int>());

            foreach (var opt in options)
            {
                var distinctValues = DistinctOptionValuesPreserveOrder(opt.Values);

                var productOption = new ProductOption
                {
                    ProductId = productId,
                    Name = opt.Name,
                    IsDeleted = false
                };
                _dbContext.ProductOption.Add(productOption);
                await _dbContext.SaveChangesAsync(cancelToken);

                if (distinctValues.Count > 0)
                {
                    foreach (var value in distinctValues)
                    {
                        _dbContext.ProductOptionValue.Add(new ProductOptionValue
                        {
                            ProductOptionId = productOption.Id,
                            Value = value
                        });
                    }
                    await _dbContext.SaveChangesAsync(cancelToken);
                }

                // "גודל" (Size) is a product variation dimension (e.g. weight-by-size), not a reusable feature — do not create a global Attribute for it
                var isVariationOnlyOption = opt.Name == "גודל" || string.Equals(opt.Name, "Size", StringComparison.OrdinalIgnoreCase);
                if (!isVariationOnlyOption)
                {
                    // Create/find Attribute and AttributeValue for each site
                    foreach (var siteId in siteIds)
                    {
                        // Find or create Attribute (use fully qualified name to avoid ambiguity)
                        var optionName = opt.Name.Trim();
                        var attribute = await _dbContext.Attribute
                            .Include(a => a.AttributeValue)
                            .FirstOrDefaultAsync(a => a.Name == optionName && a.SiteId == siteId && !a.IsDeleted, cancelToken);

                        if (attribute == null)
                        {
                            attribute = new George.DB.Attribute
                            {
                                Name = opt.Name,
                                SiteId = siteId,
                                CreationTime = DateTime.UtcNow,
                                IsDeleted = false,
                                GuidId = Guid.NewGuid()
                            };
                            _dbContext.Attribute.Add(attribute);
                            await _dbContext.SaveChangesAsync(cancelToken);
                            
                            // Reload to get AttributeValues collection
                            attribute = await _dbContext.Attribute
                                .Include(a => a.AttributeValue)
                                .FirstOrDefaultAsync(a => a.Id == attribute.Id, cancelToken);
                        }

                        // Create AttributeValues for each option value
                        if (distinctValues.Count > 0 && attribute != null)
                        {
                            foreach (var value in distinctValues)
                            {
                                // Check if AttributeValue already exists
                                var existingValue = attribute.AttributeValue
                                    .FirstOrDefault(av => av.Value == value);

                                if (existingValue == null)
                                {
                                    _dbContext.AttributeValue.Add(new AttributeValue
                                    {
                                        AttributeId = attribute.Id,
                                        Value = value
                                    });
                                }
                            }
                            await _dbContext.SaveChangesAsync(cancelToken);
                        }
                    }
                }
            }
        }

        /// <param name="limitAttributeToSiteIds">When set, create attributes only for these site IDs (e.g. bulk import target site). When null, create for all sites the product is on.</param>
        public async Task UpdateProductOptionsAsync(int productId, List<ProductOptionDto>? options, List<int>? limitAttributeToSiteIds = null, CancellationToken cancelToken = default)
        {
            if (options == null) return;

            var existingOptions = await _dbContext.ProductOption
                .Where(po => po.ProductId == productId)
                .ToListAsync(cancelToken);

            foreach (var existing in existingOptions)
            {
                existing.IsDeleted = true;
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            await CreateProductOptionsAsync(productId, options, limitAttributeToSiteIds, cancelToken);
        }

        public async Task CreateProductVariantsAsync(int productId, List<ProductVariantDto> variants, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            foreach (var variant in variants)
            {
                var productVariant = new ProductVariant
                {
                    ProductId = productId,
                    ImageUrl = variant.ImageUrl,
                    Price = variant.Price,
                    SalePrice = variant.SalePrice,
                    StockQuantity = variant.StockQuantity,
                    Sku = string.IsNullOrWhiteSpace(variant.Sku) ? null : variant.Sku,
                    Weight = variant.Weight,
                    IsDeleted = false
                };
                _dbContext.ProductVariant.Add(productVariant);
                await _dbContext.SaveChangesAsync(cancelToken);

                // Map option values if provided
                if (variant.OptionValues != null && variant.OptionValues.Any())
                {
                    foreach (var kvp in variant.OptionValues)
                    {
                        _dbContext.ProductVariantOptionValue.Add(new ProductVariantOptionValue
                        {
                            ProductVariantId = productVariant.Id,
                            OptionName = kvp.Key,
                            OptionValue = kvp.Value
                        });
                    }
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
            }
        }

        public async Task UpdateProductVariantsAsync(int productId, List<ProductVariantDto>? variants, List<ProductOptionDto>? options, CancellationToken cancelToken)
        {
            if (variants == null) return;

            var existingVariants = await _dbContext.ProductVariant
                .Where(pv => pv.ProductId == productId)
                .ToListAsync(cancelToken);

            foreach (var existing in existingVariants)
            {
                existing.IsDeleted = true;
            }
            await _dbContext.SaveChangesAsync(cancelToken);

            await CreateProductVariantsAsync(productId, variants, options, cancelToken);
        }

        public async Task<Product?> GetProductBySkuAsync(string sku, int? accountId, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;

            var query = _dbContext.Product
                .Where(p => !p.IsDeleted && p.Sku != null && p.Sku.ToLower().Trim() == sku.ToLower().Trim());

            if (accountId.HasValue)
            {
                query = query.Where(p => p.AccountId == accountId.Value);
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Finds a product by SKU, account and site(s). The product must be assigned to at least one of the given site IDs.
        /// When exclusiveSitesOnly is true, the product must not be assigned to any site outside the given list (each site has its own products; no shared product across sites).
        /// Used during bulk import so we only update a product that belongs exclusively to the target site(s); otherwise we would change data (e.g. images) for other sites.
        /// </summary>
        public async Task<Product?> GetProductBySkuAndSitesAsync(string sku, int? accountId, List<int>? siteIds, bool exclusiveSitesOnly = false, CancellationToken cancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;
            if (siteIds == null || !siteIds.Any()) return null;

            var query = _dbContext.Product
                .Where(p => !p.IsDeleted && p.Sku != null && p.Sku.ToLower().Trim() == sku.ToLower().Trim());

            if (accountId.HasValue)
            {
                query = query.Where(p => p.AccountId == accountId.Value);
            }

            query = query.Where(p => p.Site.Any(s => siteIds.Contains(s.Id)));

            if (exclusiveSitesOnly)
            {
                // Product must not be on any site outside the target list (no shared product across sites)
                query = query.Where(p => !p.Site.Any(s => !siteIds.Contains(s.Id)));
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Finds a product by name, account and optionally site(s) (case-insensitive name). Used as fallback when re-importing and SKU is empty.
        /// When siteIds is provided and has items, the product must be assigned to at least one of those sites.
        /// When exclusiveSitesOnly is true, the product must not be on any site outside the given list (each site has its own products).
        /// Returns the first match (by Id) when multiple products share the same name in the account.
        /// </summary>
        public async Task<Product?> GetProductByNameAndAccountAsync(string name, int? accountId, List<int>? siteIds, bool exclusiveSitesOnly = false, CancellationToken cancelToken = default)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var query = _dbContext.Product
                .Where(p => !p.IsDeleted && p.Name != null && p.Name.Trim().ToLower() == name.Trim().ToLower());

            if (accountId.HasValue)
            {
                query = query.Where(p => p.AccountId == accountId.Value);
            }

            if (siteIds != null && siteIds.Any())
            {
                query = query.Where(p => p.Site.Any(s => siteIds.Contains(s.Id)));
                if (exclusiveSitesOnly)
                {
                    query = query.Where(p => !p.Site.Any(s => !siteIds.Contains(s.Id)));
                }
            }

            return await query
                .OrderBy(p => p.Id)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        public async Task<WeightConfig?> CreateOrUpdateWeightConfigAsync(WeightConfigDto req, CancellationToken cancelToken)
        {
            var weightConfig = new WeightConfig
            {
                StartWeight = req.StartWeight,
                Step = req.Step,
                FixedWeightPerUnit = req.FixedWeightPerUnit,
                UnitWeight = req.UnitWeight,
                WeightOptions = req.WeightOptions,
                WeightByVariant = req.WeightByVariant,
                ShowPricePer100g = req.ShowPricePer100g,
                ShowUnitPrice = req.ShowUnitPrice,
                SoldByLabel = req.SoldByLabel,
                IsDeleted = false
            };

            if (req.Unit.HasValue())
            {
                var unit = await _dbContext.Unit
                    .FirstOrDefaultAsync(u => u.Name == req.Unit, cancelToken);
                weightConfig.UnitId = unit?.Id;
            }

            if (req.UnitWeightMode.HasValue())
            {
                var mode = await _dbContext.UnitWeightMode
                    .FirstOrDefaultAsync(m => m.Name == req.UnitWeightMode, cancelToken);
                weightConfig.UnitWeightModeId = mode?.Id;
            }

            _dbContext.WeightConfig.Add(weightConfig);
            await _dbContext.SaveChangesAsync(cancelToken);
            return weightConfig;
        }

        public async Task MapLookupsAsync(Product product, ProductLookupDto req, CancellationToken cancelToken)
        {
            // Map status
            if (req.Status.HasValue())
            {
                var statusName = req.Status;
                // Normalize status names from client
                if (statusName == "public") statusName = "active";
                if (statusName == "published") statusName = "active";
                // Do not map "draft" → "hidden": draft must stay a distinct ProductStatus so WooCommerce sync sends status "draft"
                // (hidden maps to Woo "private"). Catalog import and forms use draft for "not published yet".
                if (statusName == "archived") statusName = "hidden";

                var status = await _dbContext.ProductStatus
                    .FirstOrDefaultAsync(s => s.Name.ToLower() == statusName.ToLower().Trim(), cancelToken);
                product.StatusId = status?.Id;
                // If client sends "draft" but DB has no ProductStatus row named "draft", leave StatusId unset (null).
                // Do not fall back to "hidden": that maps to WooCommerce "private". Add a `draft` row to ProductStatus if you need draft in George.
            }

            // Map visibility
            if (req.Visibility.HasValue())
            {
                var visibilityName = req.Visibility;
                // Normalize visibility names from client
                if (visibilityName == "public") visibilityName = "active";
                if (visibilityName == "published") visibilityName = "active";
                // Do not map "draft" → "hidden": same overload problem as ProductStatus (hidden → Woo private).
                if (visibilityName == "archived") visibilityName = "hidden";

                var visibility = await _dbContext.Visibility
                    .FirstOrDefaultAsync(v => v.Name.ToLower() == visibilityName.ToLower().Trim(), cancelToken);
                product.VisibilityId = visibility?.Id;
            }

            // Map stock management type
            if (req.StockManagementType.HasValue())
            {
                var smt = await _dbContext.StockManagementType
                    .FirstOrDefaultAsync(s => s.Name == req.StockManagementType && !s.IsDeleted, cancelToken);
                
                if (smt == null)
                {
                    // Create the stock management type if it doesn't exist
                    smt = new StockManagementType
                    {
                        Name = req.StockManagementType,
                        IsDeleted = false
                    };
                    _dbContext.StockManagementType.Add(smt);
                    await _dbContext.SaveChangesAsync(cancelToken);
                }
                
                product.StockManagementTypeId = smt.Id;
            }

            // Map stock status
            if (req.StockStatus.HasValue())
            {
                var ss = await _dbContext.StockStatus
                    .FirstOrDefaultAsync(s => s.Name == req.StockStatus, cancelToken);
                product.StockStatusId = ss?.Id;
            }

            // Map shipping class
            if (req.ShippingClass.HasValue())
            {
                var sc = await _dbContext.ShippingClass
                    .FirstOrDefaultAsync(s => s.Name == req.ShippingClass, cancelToken);
                product.ShippingClassId = sc?.Id;
            }

            // Map setup type
            if (req.SetupType.HasValue())
            {
                var st = await _dbContext.SetupType
                    .FirstOrDefaultAsync(s => s.Name == req.SetupType, cancelToken);
                product.SetupTypeId = st?.Id;
            }

            // Map brands: explicit IDs replace legacy single FK via ProductBrand (handled in Create/UpdateProductAsync).
            if (req.BrandIds != null)
            {
                var ordered = DistinctPositiveIdsPreserveOrder(req.BrandIds);
                if (ordered.Count == 0)
                {
                    product.BrandId = null;
                }
                else
                {
                    var accountId = product.AccountId;
                    var valid = await _dbContext.Brand
                        .Where(b => ordered.Contains(b.Id) && !b.IsDeleted && (!accountId.HasValue || b.AccountId == accountId))
                        .Select(b => b.Id)
                        .ToListAsync(cancelToken)
                        .ConfigureAwait(false);
                    var validSet = valid.ToHashSet();
                    var first = ordered.FirstOrDefault(id => validSet.Contains(id));
                    product.BrandId = first > 0 ? first : null;
                }
            }
            // Legacy free-text brand (find/create). Only when BrandIds omitted — avoids wiping BrandId on partial updates.
            else if (req.Brand.HasValue())
            {
                var name = req.Brand.Trim();
                var accountId = product.AccountId;
                var brand = await _dbContext.Brand
                    .FirstOrDefaultAsync(b => b.Name == name && b.AccountId == accountId && !b.IsDeleted, cancelToken);
                if (brand == null)
                {
                    brand = new Brand
                    {
                        Name = name,
                        AccountId = accountId,
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                    };
                    _dbContext.Brand.Add(brand);
                    await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
                }

                product.BrandId = brand.Id;
            }

            // Map supplier — find or create Supplier for this account.
            if (req.Supplier.HasValue())
            {
                var name = req.Supplier.Trim();
                var accountId = product.AccountId;
                var supplier = await _dbContext.Supplier
                    .FirstOrDefaultAsync(s => s.Name == name && s.AccountId == accountId && !s.IsDeleted, cancelToken);
                if (supplier == null)
                {
                    supplier = new Supplier
                    {
                        Name = name,
                        AccountId = accountId,
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                    };
                    _dbContext.Supplier.Add(supplier);
                    await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
                }

                product.SupplierId = supplier.Id;
            }
            else
            {
                product.SupplierId = null;
            }

            // Map weight config
            if (req.WeightConfig != null)
            {
                var weightConfig = await CreateOrUpdateWeightConfigAsync(req.WeightConfig, cancelToken);
                product.WeightConfigId = weightConfig?.Id;
            }
        }

        /// <summary>
        /// Applies catalog stock change from picking: <c>StockQuantity -= consumptionDelta</c>.
        /// <paramref name="consumptionDelta"/> is (new picked − old picked): more picked reduces stock; negative restores stock.
        /// For weight lines, picked values are kg; for unit lines, unit count. Skips when stock is not quantity-managed.
        /// </summary>
        public async Task ApplyPickingConsumptionDeltaAsync(
            int productId,
            int? productVariantId,
            decimal consumptionDelta,
            CancellationToken cancelToken)
        {
            if (consumptionDelta == 0m) return;

            var product = await _dbContext.Product
                .Include(p => p.StockManagementType)
                .Include(p => p.ProductVariant)
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted, cancelToken)
                .ConfigureAwait(false);
            if (product == null) return;

            var smt = product.StockManagementType?.Name;
            if (string.Equals(smt, "status", StringComparison.OrdinalIgnoreCase))
                return;

            var perVariationQty = string.Equals(smt, "variation", StringComparison.OrdinalIgnoreCase)
                && product.VariationStockByQuantity == true
                && productVariantId.HasValue;

            if (perVariationQty)
            {
                var v = product.ProductVariant?.FirstOrDefault(x => x.Id == productVariantId && !x.IsDeleted);
                if (v == null) return;
                v.StockQuantity = SubtractConsumptionFromStock(v.StockQuantity, consumptionDelta);
            }
            else if (string.Equals(smt, "quantity", StringComparison.OrdinalIgnoreCase)
                || string.Equals(smt, "variation", StringComparison.OrdinalIgnoreCase))
            {
                product.StockQuantity = SubtractConsumptionFromStock(product.StockQuantity, consumptionDelta);
            }
            else
            {
                return;
            }

            product.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }

        private static decimal? SubtractConsumptionFromStock(decimal? stock, decimal consumptionDelta)
        {
            var cur = stock ?? 0m;
            var next = cur - consumptionDelta;
            if (next < 0m) next = 0m;
            return next;
        }

        /// <summary>
        /// Bulk-clear expired timed storefront labels (חדש / כשר לפסח). Same rules as
        /// <c>ProductService.ClearExpiredTimedLabels</c>, for background cleanup without opening each product.
        /// </summary>
        /// <returns>Row counts updated for passover and new-label columns.</returns>
        public async Task<(int PassoverRows, int NewLabelRows)> ClearExpiredTimedProductLabelsAsync(CancellationToken cancelToken)
        {
            var now = DateTime.UtcNow;
            var passoverRows = await _dbContext.Product
                .Where(p =>
                    !p.IsDeleted
                    && p.LabelKosherForPassover
                    && p.LabelKosherForPassoverEndDate != null
                    && p.LabelKosherForPassoverEndDate <= now)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.LabelKosherForPassover, false)
                        .SetProperty(p => p.LabelKosherForPassoverEndDate, (DateTime?)null),
                    cancelToken)
                .ConfigureAwait(false);

            var newLabelRows = await _dbContext.Product
                .Where(p =>
                    !p.IsDeleted
                    && p.LabelNew
                    && p.LabelNewEndDate != null
                    && p.LabelNewEndDate <= now)
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(p => p.LabelNew, false)
                        .SetProperty(p => p.LabelNewEndDate, (DateTime?)null),
                    cancelToken)
                .ConfigureAwait(false);

            return (passoverRows, newLabelRows);
        }

    }
}

