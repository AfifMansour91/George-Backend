using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    /// <summary>
    /// Storage / data-access layer for the GlobalBrand entity. Mirrors
    /// <see cref="GlobalCategoryStorage"/>. GlobalBrand is platform-wide and has no
    /// account / site scoping; the only relationship in v1 is self-referential parent.
    /// </summary>
    public class GlobalBrandStorage : StorageBase
    {
        public GlobalBrandStorage(GeorgeDBContext dbContext, ILogger<GlobalBrandStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<GlobalBrand>> GetGlobalBrandsAsync(
            GlobalBrandFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<GlobalBrand>();

            var query = _dbContext.GlobalBrand
                .Include(gb => gb.ParentGlobalBrand)
                .AsNoTracking();

            if (filter != null)
            {
                // 0 → root only; >0 → that parent; null → no filter
                if (filter.ParentGlobalBrandId == 0)
                {
                    query = query.Where(gb => gb.ParentGlobalBrandId == null);
                }
                else if (filter.ParentGlobalBrandId.HasValue)
                {
                    query = query.Where(gb => gb.ParentGlobalBrandId == filter.ParentGlobalBrandId.Value);
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(gb => gb.Name.Contains(term)
                                            || (gb.Slug != null && gb.Slug.Contains(term))
                                            || (gb.Description != null && gb.Description.Contains(term)));
                }
            }

            // GlobalBrand has a soft-delete query filter at the DbContext level, so explicit
            // !IsDeleted check isn't strictly needed; keep it as an extra guard.
            query = query.Where(gb => !gb.IsDeleted);

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query.OrderBy(gb => gb.SortOrder).ThenBy(gb => gb.Name);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);
            return res;
        }

        public async Task<GlobalBrand?> GetGlobalBrandAsync(int globalBrandId, CancellationToken cancelToken)
        {
            return await _dbContext.GlobalBrand
                .Include(gb => gb.ParentGlobalBrand)
                .Include(gb => gb.InverseParentGlobalBrand)
                .AsNoTracking()
                .FirstOrDefaultAsync(gb => gb.Id == globalBrandId && !gb.IsDeleted, cancelToken);
        }

        public async Task<GlobalBrand> CreateGlobalBrandAsync(GlobalBrand globalBrand, CancellationToken cancelToken)
        {
            _dbContext.GlobalBrand.Add(globalBrand);
            await _dbContext.SaveChangesAsync(cancelToken);
            return globalBrand;
        }

        public async Task<GlobalBrand?> UpdateGlobalBrandAsync(GlobalBrand updated, CancellationToken cancelToken)
        {
            var dbBrand = await _dbContext.GlobalBrand
                .FirstOrDefaultAsync(gb => gb.Id == updated.Id && !gb.IsDeleted, cancelToken);

            if (dbBrand == null) return null;

            dbBrand.Name = updated.Name;
            dbBrand.Slug = updated.Slug;
            dbBrand.Description = updated.Description;
            dbBrand.ParentGlobalBrandId = updated.ParentGlobalBrandId;
            dbBrand.SortOrder = updated.SortOrder;
            dbBrand.ProductCount = updated.ProductCount;
            dbBrand.ImageUrl = updated.ImageUrl;
            dbBrand.IconUrl = updated.IconUrl;
            dbBrand.SeoTitle = updated.SeoTitle;
            dbBrand.SeoDescription = updated.SeoDescription;
            dbBrand.WooCommerceBrandId = updated.WooCommerceBrandId;
            dbBrand.UpdatedDate = DateTime.UtcNow;
            dbBrand.UpdateUserId = updated.UpdateUserId;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbBrand;
        }

        public async Task<bool> DeleteGlobalBrandAsync(int globalBrandId, CancellationToken cancelToken)
        {
            var globalBrand = await _dbContext.GlobalBrand
                .FirstOrDefaultAsync(gb => gb.Id == globalBrandId && !gb.IsDeleted, cancelToken);

            if (globalBrand == null) return false;

            globalBrand.IsDeleted = true;
            globalBrand.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Update only the WooCommerce id (called by sync code after a push).
        /// </summary>
        public async Task<bool> UpdateGlobalBrandWooCommerceIdAsync(int globalBrandId, int? wooCommerceBrandId, CancellationToken cancelToken)
        {
            var globalBrand = await _dbContext.GlobalBrand
                .FirstOrDefaultAsync(gb => gb.Id == globalBrandId && !gb.IsDeleted, cancelToken);

            if (globalBrand == null) return false;

            globalBrand.WooCommerceBrandId = wooCommerceBrandId;
            globalBrand.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Find a GlobalBrand by name (case-insensitive, trimmed) for de-duplication on create.
        /// Optionally constrained by parent.
        /// </summary>
        public async Task<GlobalBrand?> FindGlobalBrandByNameAsync(
            string name,
            int? parentGlobalBrandId,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var normalized = name.Trim().ToLower();

            var query = _dbContext.GlobalBrand
                .Where(gb => !gb.IsDeleted && gb.Name.ToLower().Trim() == normalized);

            if (parentGlobalBrandId.HasValue)
            {
                query = query.Where(gb => gb.ParentGlobalBrandId == parentGlobalBrandId.Value);
            }
            else
            {
                query = query.Where(gb => gb.ParentGlobalBrandId == null);
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Find GlobalBrand by its WooCommerce taxonomy id. Used during pull-from-WooCommerce sync.
        /// </summary>
        public async Task<GlobalBrand?> FindGlobalBrandByWooCommerceIdAsync(
            int wooCommerceBrandId,
            CancellationToken cancelToken)
        {
            return await _dbContext.GlobalBrand
                .Where(gb => !gb.IsDeleted && gb.WooCommerceBrandId == wooCommerceBrandId)
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }
    }
}
