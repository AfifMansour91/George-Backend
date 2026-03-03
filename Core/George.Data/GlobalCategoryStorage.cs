using George.Common;
using George.Common.Request;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class GlobalCategoryStorage : StorageBase
    {
        public GlobalCategoryStorage(GeorgeDBContext dbContext, ILogger<GlobalCategoryStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<GlobalCategory>> GetGlobalCategoriesAsync(
            GlobalCategoryFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<GlobalCategory>();

            var query = _dbContext.GlobalCategory
                .Include(gc => gc.ParentGlobalCategory)
                .Include(gc => gc.BusinessType.Where(bt => !bt.IsDeleted))
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.ParentGlobalCategoryId.HasValue)
                {
                    query = query.Where(gc => gc.ParentGlobalCategoryId == filter.ParentGlobalCategoryId.Value);
                }
                else if (filter.ParentGlobalCategoryId == 0)
                {
                    // Explicitly request root categories (no parent)
                    query = query.Where(gc => gc.ParentGlobalCategoryId == null);
                }

                if (filter.BusinessTypeId.HasValue)
                {
                    query = query.Where(gc => gc.BusinessType.Any(bt => bt.Id == filter.BusinessTypeId.Value && !bt.IsDeleted));
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(gc => gc.Name.Contains(term) ||
                                           (gc.Description != null && gc.Description.Contains(term)));
                }
            }

            // Only get non-deleted categories
            query = query.Where(gc => !gc.IsDeleted);

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting - by SortOrder, then by Name
            query = query.OrderBy(gc => gc.SortOrder).ThenBy(gc => gc.Name);

            // Add paging
            //query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<GlobalCategory?> GetGlobalCategoryAsync(int globalCategoryId, CancellationToken cancelToken)
        {
            return await _dbContext.GlobalCategory
                .Include(gc => gc.ParentGlobalCategory)
                .Include(gc => gc.BusinessType.Where(bt => !bt.IsDeleted))
                .Include(gc => gc.InverseParentGlobalCategory)
                .AsNoTracking()
                .FirstOrDefaultAsync(gc => gc.Id == globalCategoryId && !gc.IsDeleted, cancelToken);
        }

        public async Task<GlobalCategory> CreateGlobalCategoryAsync(
            GlobalCategory globalCategory,
            List<int>? businessTypeIds,
            CancellationToken cancelToken)
        {
            _dbContext.GlobalCategory.Add(globalCategory);

            // Add business types if provided (only non-deleted)
            if (businessTypeIds != null && businessTypeIds.Any())
            {
                var businessTypes = await _dbContext.BusinessType
                    .Where(bt => businessTypeIds.Contains(bt.Id) && !bt.IsDeleted)
                    .ToListAsync(cancelToken);
                
                foreach (var businessType in businessTypes)
                {
                    globalCategory.BusinessType.Add(businessType);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return globalCategory;
        }

        public async Task<GlobalCategory?> UpdateGlobalCategoryAsync(
            GlobalCategory updated,
            List<int>? businessTypeIds,
            CancellationToken cancelToken)
        {
            var dbGlobalCategory = await _dbContext.GlobalCategory
                .Include(gc => gc.BusinessType.Where(bt => !bt.IsDeleted))
                .FirstOrDefaultAsync(gc => gc.Id == updated.Id && !gc.IsDeleted, cancelToken);

            if (dbGlobalCategory == null) return null;

            // Update basic properties
            dbGlobalCategory.Name = updated.Name;
            dbGlobalCategory.Description = updated.Description;
            dbGlobalCategory.ParentGlobalCategoryId = updated.ParentGlobalCategoryId;
            dbGlobalCategory.SortOrder = updated.SortOrder;
            dbGlobalCategory.ProductCount = updated.ProductCount;
            dbGlobalCategory.ImageUrl = updated.ImageUrl;
            dbGlobalCategory.IconUrl = updated.IconUrl;
            dbGlobalCategory.UpdatedDate = DateTime.UtcNow;
            dbGlobalCategory.UpdateUserId = updated.UpdateUserId;

            // Update business types
            if (businessTypeIds != null)
            {
                dbGlobalCategory.BusinessType.Clear();
                if (businessTypeIds.Any())
                {
                    var businessTypes = await _dbContext.BusinessType
                        .Where(bt => businessTypeIds.Contains(bt.Id) && !bt.IsDeleted)
                        .ToListAsync(cancelToken);
                    
                    foreach (var businessType in businessTypes)
                    {
                        dbGlobalCategory.BusinessType.Add(businessType);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbGlobalCategory;
        }

        public async Task<bool> DeleteGlobalCategoryAsync(int globalCategoryId, CancellationToken cancelToken)
        {
            var globalCategory = await _dbContext.GlobalCategory
                .FirstOrDefaultAsync(gc => gc.Id == globalCategoryId && !gc.IsDeleted, cancelToken);

            if (globalCategory == null) return false;

            globalCategory.IsDeleted = true;
            globalCategory.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Find global category by name, optionally with parent
        /// </summary>
        public async Task<GlobalCategory?> FindGlobalCategoryByNameAsync(
            string name,
            int? parentGlobalCategoryId,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            var query = _dbContext.GlobalCategory
                .Where(gc => !gc.IsDeleted && gc.Name.ToLower().Trim() == name.ToLower().Trim());

            if (parentGlobalCategoryId.HasValue)
            {
                query = query.Where(gc => gc.ParentGlobalCategoryId == parentGlobalCategoryId.Value);
            }
            else
            {
                query = query.Where(gc => gc.ParentGlobalCategoryId == null);
            }

            return await query
                .AsNoTracking()
                .FirstOrDefaultAsync(cancelToken);
        }

        /// <summary>
        /// Find or create global category by hierarchical path (e.g., "Parent > Child")
        /// </summary>
        public async Task<GlobalCategory?> FindOrCreateGlobalCategoryByPathAsync(
            string categoryPath,
            int? creationUserId,
            CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(categoryPath)) return null;

            var parts = categoryPath.Split('>')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            if (!parts.Any()) return null;

            GlobalCategory? currentCategory = null;

            foreach (var part in parts)
            {
                var parentId = currentCategory?.Id;
                var existing = await FindGlobalCategoryByNameAsync(part, parentId, cancelToken);

                if (existing != null)
                {
                    currentCategory = existing;
                }
                else
                {
                    // Create new global category
                    var newCategory = new GlobalCategory
                    {
                        Name = part,
                        ParentGlobalCategoryId = parentId,
                        Description = null,
                        IsDeleted = false,
                        CreationTime = DateTime.UtcNow,
                        CreationUserId = creationUserId,
                        GuidId = Guid.NewGuid(),
                        SortOrder = 0
                    };

                    currentCategory = await CreateGlobalCategoryAsync(newCategory, null, cancelToken);
                }
            }

            return currentCategory;
        }
    }
}

