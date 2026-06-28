using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class CategoryStorage : StorageBase
    {
        public CategoryStorage(GeorgeDBContext dbContext, ILogger<CategoryStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Category>> GetCategoriesAsync(
            CategoryFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Category>();

            var query = _dbContext.Category
                .Include(c => c.Account)
                .Include(c => c.ParentCategory)
                .Include(c => c.Site)
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.AccountId.HasValue)
                {
                    query = query.Where(c => c.AccountId == filter.AccountId.Value);
                }

                if (filter.SiteId.HasValue)
                {
                    query = query.Where(c => c.Site.Any(s => s.Id == filter.SiteId.Value));
                }

                // 0 means root categories (no parent); otherwise filter by that parent id
                if (filter.ParentCategoryId == 0)
                {
                    query = query.Where(c => c.ParentCategoryId == null);
                }
                else if (filter.ParentCategoryId.HasValue)
                {
                    query = query.Where(c => c.ParentCategoryId == filter.ParentCategoryId.Value);
                }

                if (filter.IsEnabled.HasValue)
                {
                    query = query.Where(c => c.IsEnabled == filter.IsEnabled.Value);
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(c => c.Name.Contains(term) ||
                                           (c.Description != null && c.Description.Contains(term)));
                }
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting
            query = query.OrderBy(c => c.SortOrder).ThenBy(c => c.Name);

            // Add paging
            //query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Category?> GetCategoryAsync(int categoryId, CancellationToken cancelToken)
        {
            return await _dbContext.Category
                .Include(c => c.Account)
                .Include(c => c.ParentCategory)
                .Include(c => c.Site)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);
        }

        public async Task<Category> CreateCategoryAsync(Category category, List<int>? siteIds, CancellationToken cancelToken)
        {
            _dbContext.Category.Add(category);

            // When no site list is provided ("all sites"), link the category to ALL of the account's sites
            // (mirrors product creation). A category linked to NO site is invisible per-branch in the UI and is
            // skipped by the per-site WooCommerce category sync, so empty must expand rather than stay unlinked.
            var effectiveSiteIds = (siteIds != null && siteIds.Any())
                ? siteIds
                : (category.AccountId.HasValue
                    ? await _dbContext.Site
                        .Where(s => s.AccountId == category.AccountId.Value && !s.IsDeleted)
                        .Select(s => s.Id)
                        .ToListAsync(cancelToken)
                    : null);

            if (effectiveSiteIds != null && effectiveSiteIds.Any())
            {
                var sites = await _dbContext.Site
                    .Where(s => effectiveSiteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);

                // A category is owned by the account of the sites it lives in. Realign AccountId to those sites so it
                // never points at the acting user's home account (e.g. a master/admin creating a category for another
                // account's branch) — otherwise the account-scoped category list hides it from that branch's products.
                var siteAccountId = sites.Select(s => (int?)s.AccountId).FirstOrDefault(a => a.HasValue);
                if (siteAccountId.HasValue)
                    category.AccountId = siteAccountId;

                foreach (var site in sites)
                {
                    category.Site.Add(site);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return category;
        }

        public async Task<Category?> UpdateCategoryAsync(Category updated, List<int>? siteIds, CancellationToken cancelToken)
        {
            var dbCategory = await _dbContext.Category
                .Include(c => c.Site)
                .FirstOrDefaultAsync(c => c.Id == updated.Id, cancelToken);

            if (dbCategory == null) return null;

            // Update basic properties
            dbCategory.Name = updated.Name;
            dbCategory.ParentCategoryId = updated.ParentCategoryId;
            dbCategory.Description = updated.Description;
            dbCategory.CustomName = updated.CustomName;
            dbCategory.IsEnabled = updated.IsEnabled;
            dbCategory.SortOrder = updated.SortOrder;
            dbCategory.DisplayAsMain = updated.DisplayAsMain;
            dbCategory.AccountId = updated.AccountId ?? dbCategory.AccountId;
            dbCategory.ImageUrl = updated.ImageUrl;
            dbCategory.IconUrl = updated.IconUrl;
            dbCategory.ShowInKiosk = updated.ShowInKiosk;
            dbCategory.KioskDisplayOrder = updated.KioskDisplayOrder;
            dbCategory.UpdatedDate = DateTime.UtcNow;
            dbCategory.UpdateUserId = updated.UpdateUserId;

            // Update sites
            if (siteIds != null)
            {
                dbCategory.Site.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Site
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);

                    // Realign ownership to the account of the (new) sites — see CreateCategoryAsync. This also repairs
                    // legacy rows whose AccountId was wrong (e.g. a master's home account) the next time they are saved.
                    var siteAccountId = sites.Select(s => (int?)s.AccountId).FirstOrDefault(a => a.HasValue);
                    if (siteAccountId.HasValue)
                        dbCategory.AccountId = siteAccountId;

                    foreach (var site in sites)
                    {
                        dbCategory.Site.Add(site);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbCategory;
        }

        /// <summary>
        /// Removes the category from the given site only (unlinks CategorySite). Other sites keep the category.
        /// If this was the last site linked to the category, soft-deletes the category.
        /// </summary>
        public async Task<bool> RemoveCategoryFromSiteAsync(int categoryId, int siteId, CancellationToken cancelToken)
        {
            var category = await _dbContext.Category
                .Include(c => c.Site)
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);

            if (category == null) return false;

            var siteToRemove = category.Site.FirstOrDefault(s => s.Id == siteId);
            if (siteToRemove == null) return true; // already not on this site

            category.Site.Remove(siteToRemove);
            category.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);

            // If that was the last site, soft-delete the category
            if (!category.Site.Any())
            {
                category.IsDeleted = true;
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return true;
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancelToken)
        {
            var category = await _dbContext.Category
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);

            if (category == null) return false;

            category.IsDeleted = true;
            category.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> UpdateCategoryWooCommerceIdAsync(int categoryId, int? wooCommerceId, CancellationToken cancelToken)
        {
            var category = await _dbContext.Category
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);

            if (category == null) return false;

            category.WooCommerceId = wooCommerceId;
            category.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Find category by name, optionally with parent and site filters
        /// </summary>
        public async Task<Category?> FindCategoryByNameAsync(
            string name,
            int? parentCategoryId,
            int? accountId,
            List<int>? siteIds,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var query = _dbContext.Category
                .Where(c => !c.IsDeleted && c.Name.ToLower().Trim() == name.ToLower().Trim());

            if (parentCategoryId.HasValue)
            {
                query = query.Where(c => c.ParentCategoryId == parentCategoryId.Value);
            }
            else
            {
                query = query.Where(c => c.ParentCategoryId == null);
            }

            if (accountId.HasValue)
            {
                query = query.Where(c => c.AccountId == accountId.Value);
            }

            if (siteIds != null && siteIds.Any())
            {
                query = query.Where(c => c.Site.Any(s => siteIds.Contains(s.Id)) || !c.Site.Any());
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Ensures the category is linked to the given site(s). Adds any missing sites; does not remove existing ones.
        /// </summary>
        public async Task EnsureCategoryHasSitesAsync(int categoryId, List<int>? siteIds, CancellationToken cancelToken)
        {
            if (siteIds == null || !siteIds.Any()) return;

            var dbCategory = await _dbContext.Category
                .Include(c => c.Site)
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);

            if (dbCategory == null) return;

            var existingSiteIds = dbCategory.Site.Select(s => s.Id).ToHashSet();
            var toAdd = siteIds.Where(id => !existingSiteIds.Contains(id)).ToList();
            if (toAdd.Count == 0) return;

            var sites = await _dbContext.Site
                .Where(s => toAdd.Contains(s.Id))
                .ToListAsync(cancelToken);
            foreach (var site in sites)
            {
                dbCategory.Site.Add(site);
            }
            await _dbContext.SaveChangesAsync(cancelToken);
        }

        /// <summary>
        /// Find or create category by hierarchical path (e.g., "Parent > Child").
        /// Looks up by account and parent+name first (optionally scoped to site). If not found on target site, looks up in account without site filter to avoid duplicate (AccountId, ParentId, Name); if found, links category to target site(s) and returns it.
        /// </summary>
        public async Task<Category?> FindOrCreateCategoryByPathAsync(
            string categoryPath,
            int? accountId,
            List<int>? siteIds,
            int? creationUserId,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(categoryPath)) return null;

            var parts = categoryPath.Split('>')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (!parts.Any()) return null;

            // "All sites" import sends no site list. Link categories to ALL of the account's sites (mirrors
            // product creation in ProductStorage). Without this the category is linked to NO site, so it neither
            // shows under any branch in the UI nor gets picked up by the per-site WooCommerce category sync
            // (which filters categories by Site.Any(s => s.Id == siteId)).
            var effectiveSiteIds = (siteIds != null && siteIds.Any())
                ? siteIds
                : (accountId.HasValue
                    ? await _dbContext.Site
                        .Where(s => s.AccountId == accountId.Value && !s.IsDeleted)
                        .Select(s => s.Id)
                        .ToListAsync(cancelToken)
                    : siteIds);

            Category? currentCategory = null;

            foreach (var part in parts)
            {
                var parentId = currentCategory?.Id;
                var existing = await FindCategoryByNameAsync(part, parentId, accountId, effectiveSiteIds, cancelToken);

                if (existing != null)
                {
                    // Existing category may have been created on a subset of sites; ensure it covers the target sites.
                    await EnsureCategoryHasSitesAsync(existing.Id, effectiveSiteIds, cancelToken);
                    currentCategory = await GetCategoryAsync(existing.Id, cancelToken);
                }
                else
                {
                    // Not found on target site(s); find by account + parent + name only to avoid duplicate key (same category may exist on another site)
                    existing = await FindCategoryByNameAsync(part, parentId, accountId, null, cancelToken);
                    if (existing != null)
                    {
                        await EnsureCategoryHasSitesAsync(existing.Id, effectiveSiteIds, cancelToken);
                        currentCategory = await GetCategoryAsync(existing.Id, cancelToken);
                    }
                    else
                    {
                        // Create new category
                        var newCategory = new Category
                        {
                            Name = part,
                            ParentCategoryId = parentId,
                            AccountId = accountId,
                            Description = null,
                            IsEnabled = true,
                            IsDeleted = false,
                            IsActive = true,
                            CreationTime = DateTime.UtcNow,
                            CreationUserId = creationUserId,
                            GuidId = Guid.NewGuid()
                        };

                        currentCategory = await CreateCategoryAsync(newCategory, effectiveSiteIds, cancelToken);
                    }
                }
            }

            return currentCategory;
        }

        /// <summary>George category id → WooCommerce category id for webhook id mapping.</summary>
        public async Task<Dictionary<int, int>> GetWooCommerceIdMapForCategoryIdsAsync(
            IReadOnlyList<int> categoryIds,
            CancellationToken cancelToken)
        {
            if (categoryIds.Count == 0) return new Dictionary<int, int>();

            return await _dbContext.Category
                .AsNoTracking()
                .Where(c => categoryIds.Contains(c.Id) && !c.IsDeleted && c.WooCommerceId != null && c.WooCommerceId > 0)
                .ToDictionaryAsync(c => c.Id, c => c.WooCommerceId!.Value, cancelToken)
                .ConfigureAwait(false);
        }
    }
}
