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

            var query = _dbContext.Categories
                .Include(c => c.Account)
                .Include(c => c.ParentCategory)
                .Include(c => c.Sites)
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
                    query = query.Where(c => c.Sites.Any(s => s.Id == filter.SiteId.Value));
                }

                if (filter.ParentCategoryId.HasValue)
                {
                    query = query.Where(c => c.ParentCategoryId == filter.ParentCategoryId.Value);
                }
                else if (filter.ParentCategoryId == 0)
                {
                    // Explicitly request root categories (no parent)
                    query = query.Where(c => c.ParentCategoryId == null);
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
            return await _dbContext.Categories
                .Include(c => c.Account)
                .Include(c => c.ParentCategory)
                .Include(c => c.Sites)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);
        }

        public async Task<Category> CreateCategoryAsync(Category category, List<int>? siteIds, CancellationToken cancelToken)
        {
            _dbContext.Categories.Add(category);

            // Add sites if provided
            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Sites
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                
                foreach (var site in sites)
                {
                    category.Sites.Add(site);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return category;
        }

        public async Task<Category?> UpdateCategoryAsync(Category updated, List<int>? siteIds, CancellationToken cancelToken)
        {
            var dbCategory = await _dbContext.Categories
                .Include(c => c.Sites)
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
            dbCategory.AccountId = updated.AccountId;
            dbCategory.UpdatedDate = DateTime.UtcNow;
            dbCategory.UpdateUserId = updated.UpdateUserId;

            // Update sites
            if (siteIds != null)
            {
                dbCategory.Sites.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Sites
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);
                    
                    foreach (var site in sites)
                    {
                        dbCategory.Sites.Add(site);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbCategory;
        }

        public async Task<bool> DeleteCategoryAsync(int categoryId, CancellationToken cancelToken)
        {
            var category = await _dbContext.Categories
                .FirstOrDefaultAsync(c => c.Id == categoryId, cancelToken);

            if (category == null) return false;

            category.IsDeleted = true;
            category.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> UpdateCategoryWooCommerceIdAsync(int categoryId, int? wooCommerceId, CancellationToken cancelToken)
        {
            var category = await _dbContext.Categories
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

            var query = _dbContext.Categories
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
                query = query.Where(c => c.Sites.Any(s => siteIds.Contains(s.Id)) || !c.Sites.Any());
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Find or create category by hierarchical path (e.g., "Parent > Child")
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

            Category? currentCategory = null;

            foreach (var part in parts)
            {
                var parentId = currentCategory?.Id;
                var existing = await FindCategoryByNameAsync(part, parentId, accountId, siteIds, cancelToken);

                if (existing != null)
                {
                    currentCategory = existing;
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

                    currentCategory = await CreateCategoryAsync(newCategory, siteIds, cancelToken);
                }
            }

            return currentCategory;
        }
    }
}
