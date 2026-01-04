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

            var query = _dbContext.GlobalCategories
                .Include(gc => gc.ParentGlobalCategory)
                .Include(gc => gc.BusinessTypes)
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
                    query = query.Where(gc => gc.BusinessTypes.Any(bt => bt.Id == filter.BusinessTypeId.Value));
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
            query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<GlobalCategory?> GetGlobalCategoryAsync(int globalCategoryId, CancellationToken cancelToken)
        {
            return await _dbContext.GlobalCategories
                .Include(gc => gc.ParentGlobalCategory)
                .Include(gc => gc.BusinessTypes)
                .Include(gc => gc.InverseParentGlobalCategory)
                .AsNoTracking()
                .FirstOrDefaultAsync(gc => gc.Id == globalCategoryId && !gc.IsDeleted, cancelToken);
        }

        public async Task<GlobalCategory> CreateGlobalCategoryAsync(
            GlobalCategory globalCategory,
            List<int>? businessTypeIds,
            CancellationToken cancelToken)
        {
            _dbContext.GlobalCategories.Add(globalCategory);

            // Add business types if provided
            if (businessTypeIds != null && businessTypeIds.Any())
            {
                var businessTypes = await _dbContext.BusinessTypes
                    .Where(bt => businessTypeIds.Contains(bt.Id))
                    .ToListAsync(cancelToken);
                
                foreach (var businessType in businessTypes)
                {
                    globalCategory.BusinessTypes.Add(businessType);
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
            var dbGlobalCategory = await _dbContext.GlobalCategories
                .Include(gc => gc.BusinessTypes)
                .FirstOrDefaultAsync(gc => gc.Id == updated.Id && !gc.IsDeleted, cancelToken);

            if (dbGlobalCategory == null) return null;

            // Update basic properties
            dbGlobalCategory.Name = updated.Name;
            dbGlobalCategory.Description = updated.Description;
            dbGlobalCategory.ParentGlobalCategoryId = updated.ParentGlobalCategoryId;
            dbGlobalCategory.SortOrder = updated.SortOrder;
            dbGlobalCategory.ProductCount = updated.ProductCount;
            dbGlobalCategory.UpdatedDate = DateTime.UtcNow;
            dbGlobalCategory.UpdateUserId = updated.UpdateUserId;

            // Update business types
            if (businessTypeIds != null)
            {
                dbGlobalCategory.BusinessTypes.Clear();
                if (businessTypeIds.Any())
                {
                    var businessTypes = await _dbContext.BusinessTypes
                        .Where(bt => businessTypeIds.Contains(bt.Id))
                        .ToListAsync(cancelToken);
                    
                    foreach (var businessType in businessTypes)
                    {
                        dbGlobalCategory.BusinessTypes.Add(businessType);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbGlobalCategory;
        }

        public async Task<bool> DeleteGlobalCategoryAsync(int globalCategoryId, CancellationToken cancelToken)
        {
            var globalCategory = await _dbContext.GlobalCategories
                .FirstOrDefaultAsync(gc => gc.Id == globalCategoryId && !gc.IsDeleted, cancelToken);

            if (globalCategory == null) return false;

            globalCategory.IsDeleted = true;
            globalCategory.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }
    }
}

