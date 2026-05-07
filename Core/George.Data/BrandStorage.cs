using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    /// <summary>
    /// Storage / data-access layer for the Brand entity. Mirrors <see cref="CategoryStorage"/>.
    /// Brand is account-scoped; the BrandSite shadow join controls per-site visibility.
    /// </summary>
    public class BrandStorage : StorageBase
    {
        public BrandStorage(GeorgeDBContext dbContext, ILogger<BrandStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Brand>> GetBrandsAsync(
            BrandFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Brand>();

            var query = _dbContext.Brand
                .Where(b => !b.IsDeleted)
                .Include(b => b.Account)
                .Include(b => b.ParentBrand)
                .Include(b => b.Site)
                .AsNoTracking();

            // Apply filters.
            if (filter != null)
            {
                if (filter.AccountId.HasValue)
                {
                    query = query.Where(b => b.AccountId == filter.AccountId.Value);
                }

                if (filter.SiteId.HasValue)
                {
                    query = query.Where(b => b.Site.Any(s => s.Id == filter.SiteId.Value));
                }

                // 0 means root-level (no parent); otherwise filter by that parent id.
                if (filter.ParentBrandId == 0)
                {
                    query = query.Where(b => b.ParentBrandId == null);
                }
                else if (filter.ParentBrandId.HasValue)
                {
                    query = query.Where(b => b.ParentBrandId == filter.ParentBrandId.Value);
                }

                if (filter.IsEnabled.HasValue)
                {
                    query = query.Where(b => b.IsEnabled == filter.IsEnabled.Value);
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(b => b.Name.Contains(term)
                                            || (b.Slug != null && b.Slug.Contains(term))
                                            || (b.Description != null && b.Description.Contains(term)));
                }
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Sort: SortOrder then Name.
            query = query.OrderBy(b => b.SortOrder).ThenBy(b => b.Name);

            // Note: paging is intentionally not applied here; matches CategoryStorage. Add Skip/Take
            // here if/when the consumer pages results.

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Brand?> GetBrandAsync(int brandId, CancellationToken cancelToken)
        {
            return await _dbContext.Brand
                .Where(b => !b.IsDeleted)
                .Include(b => b.Account)
                .Include(b => b.ParentBrand)
                .Include(b => b.Site)
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.Id == brandId, cancelToken);
        }

        public async Task<Brand> CreateBrandAsync(Brand brand, List<int>? siteIds, CancellationToken cancelToken)
        {
            _dbContext.Brand.Add(brand);

            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Site
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);

                foreach (var site in sites)
                {
                    brand.Site.Add(site);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return brand;
        }

        public async Task<Brand?> UpdateBrandAsync(Brand updated, List<int>? siteIds, CancellationToken cancelToken)
        {
            var dbBrand = await _dbContext.Brand
                .Include(b => b.Site)
                .FirstOrDefaultAsync(b => b.Id == updated.Id && !b.IsDeleted, cancelToken);

            if (dbBrand == null) return null;

            // Scalar fields
            dbBrand.Name = updated.Name;
            dbBrand.Slug = updated.Slug;
            dbBrand.Description = updated.Description;
            dbBrand.ParentBrandId = updated.ParentBrandId;
            dbBrand.SortOrder = updated.SortOrder;
            dbBrand.IsEnabled = updated.IsEnabled;
            dbBrand.AccountId = updated.AccountId;
            dbBrand.ImageUrl = updated.ImageUrl;
            dbBrand.IconUrl = updated.IconUrl;
            dbBrand.SeoTitle = updated.SeoTitle;
            dbBrand.SeoDescription = updated.SeoDescription;
            dbBrand.WooCommerceBrandId = updated.WooCommerceBrandId;
            dbBrand.SourceGlobalBrandId = updated.SourceGlobalBrandId;
            dbBrand.UpdatedDate = DateTime.UtcNow;
            dbBrand.UpdateUserId = updated.UpdateUserId;

            // Sites
            if (siteIds != null)
            {
                dbBrand.Site.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Site
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);

                    foreach (var site in sites)
                    {
                        dbBrand.Site.Add(site);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbBrand;
        }

        /// <summary>
        /// Removes the brand from a single site (unlinks BrandSite). If that was the last site,
        /// soft-deletes the brand. Mirrors CategoryStorage.RemoveCategoryFromSiteAsync.
        /// </summary>
        public async Task<bool> RemoveBrandFromSiteAsync(int brandId, int siteId, CancellationToken cancelToken)
        {
            var brand = await _dbContext.Brand
                .Include(b => b.Site)
                .FirstOrDefaultAsync(b => b.Id == brandId && !b.IsDeleted, cancelToken);

            if (brand == null) return false;

            var siteToRemove = brand.Site.FirstOrDefault(s => s.Id == siteId);
            if (siteToRemove == null) return true; // already not linked to this site

            brand.Site.Remove(siteToRemove);
            brand.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);

            if (!brand.Site.Any())
            {
                brand.IsDeleted = true;
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return true;
        }

        public async Task<bool> DeleteBrandAsync(int brandId, CancellationToken cancelToken)
        {
            var brand = await _dbContext.Brand
                .FirstOrDefaultAsync(b => b.Id == brandId && !b.IsDeleted, cancelToken);

            if (brand == null) return false;

            brand.IsDeleted = true;
            brand.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Update only the WooCommerce id (called by sync code after a push).
        /// </summary>
        public async Task<bool> UpdateBrandWooCommerceIdAsync(int brandId, int? wooCommerceBrandId, CancellationToken cancelToken)
        {
            var brand = await _dbContext.Brand
                .FirstOrDefaultAsync(b => b.Id == brandId && !b.IsDeleted, cancelToken);

            if (brand == null) return false;

            brand.WooCommerceBrandId = wooCommerceBrandId;
            brand.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Find a brand by name within a scope (case-insensitive, trimmed). Used for de-duplication
        /// before insert (per spec §7).
        /// </summary>
        public async Task<Brand?> FindBrandByNameAsync(
            string name,
            int? parentBrandId,
            int? accountId,
            List<int>? siteIds,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var normalized = name.Trim().ToLower();

            var query = _dbContext.Brand
                .Where(b => !b.IsDeleted && b.Name.ToLower().Trim() == normalized);

            if (parentBrandId.HasValue)
            {
                query = query.Where(b => b.ParentBrandId == parentBrandId.Value);
            }
            else
            {
                query = query.Where(b => b.ParentBrandId == null);
            }

            if (accountId.HasValue)
            {
                query = query.Where(b => b.AccountId == accountId.Value);
            }

            if (siteIds != null && siteIds.Any())
            {
                // Match if linked to any of the given sites OR brand has no site links yet.
                query = query.Where(b => b.Site.Any(s => siteIds.Contains(s.Id)) || !b.Site.Any());
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Find brand by its WooCommerce taxonomy id. Used during pull-from-WooCommerce sync.
        /// </summary>
        public async Task<Brand?> FindBrandByWooCommerceIdAsync(
            int wooCommerceBrandId,
            int? accountId,
            CancellationToken cancelToken)
        {
            var query = _dbContext.Brand
                .Where(b => !b.IsDeleted && b.WooCommerceBrandId == wooCommerceBrandId);

            if (accountId.HasValue)
                query = query.Where(b => b.AccountId == accountId.Value);

            return await query.AsNoTracking().FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Ensures the brand is linked to the given site(s). Adds any missing links; doesn't remove existing ones.
        /// Mirrors CategoryStorage.EnsureCategoryHasSitesAsync.
        /// </summary>
        public async Task EnsureBrandHasSitesAsync(int brandId, List<int>? siteIds, CancellationToken cancelToken)
        {
            if (siteIds == null || !siteIds.Any()) return;

            var dbBrand = await _dbContext.Brand
                .Include(b => b.Site)
                .FirstOrDefaultAsync(b => b.Id == brandId && !b.IsDeleted, cancelToken);

            if (dbBrand == null) return;

            var existing = dbBrand.Site.Select(s => s.Id).ToHashSet();
            var toAdd = siteIds.Where(id => !existing.Contains(id)).ToList();
            if (toAdd.Count == 0) return;

            var sites = await _dbContext.Site
                .Where(s => toAdd.Contains(s.Id))
                .ToListAsync(cancelToken);
            foreach (var site in sites)
            {
                dbBrand.Site.Add(site);
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>
        /// Returns the count of (non-deleted) products linked to this brand via the ProductBrand
        /// join table (NOT the legacy Product.BrandId column — new code goes through the join).
        /// </summary>
        public async Task<int> CountProductsAsync(int brandId, CancellationToken cancelToken)
        {
            return await _dbContext.ProductBrand
                .Where(pb => pb.BrandId == brandId && !pb.Product.IsDeleted)
                .CountAsync(cancelToken);
        }
    }
}
