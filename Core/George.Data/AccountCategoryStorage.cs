using George.Common;
using George.Common.Request;
using George.Data.Dto;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class AccountCategoryStorage : StorageBase
    {
        public AccountCategoryStorage(GeorgeDBContext dbContext, ILogger<AccountCategoryStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<List<AccountCategoryDto>> GetAccountCategoriesAsync(long accountId, CancellationToken cancelToken)
        {
            // We want: AccountCategory + master Category name.
            var query =
                from ac in _dbContext.AccountCategories.AsNoTracking()
                join c in _dbContext.Categories.AsNoTracking()
                    on ac.CategoryId equals c.Id
                where ac.AccountId == accountId
                select new AccountCategoryDto
                {
                    AccountCategoryId = ac.Id,
                    ParentAccountCategoryId = ac.ParentAccountCategoryId,
                    DisplayName = ac.CustomName ?? c.Name,
                    CustomName = ac.CustomName,
                    SortOrder = ac.SortOrder,
                    IsEnabled = ac.IsEnabled,
                    MasterCategoryId = ac.CategoryId
                };

            return await query
                .OrderBy(x => x.ParentAccountCategoryId)
                .ThenBy(x => x.SortOrder)
                .ToListAsync(cancelToken);
        }

        public async Task<bool> BulkUpdateAccountCategoriesAsync(long accountId, List<AccountCategoryUpdateItem> updates, CancellationToken cancelToken)
        {
            if (!updates.HasValue()) return true;

            var ids = updates.Select(u => u.AccountCategoryId).ToList();
            var rows = await _dbContext.AccountCategories
                .Where(x => x.AccountId == accountId && ids.Contains(x.Id))
                .ToListAsync(cancelToken);

            foreach (var row in rows)
            {
                var u = updates.First(x => x.AccountCategoryId == row.Id);

                if (u.CustomNameSet && u.CustomName != row.CustomName)
                    row.CustomName = u.CustomName;

                if (u.IsEnabledSet && u.IsEnabled != row.IsEnabled)
                    row.IsEnabled = u.IsEnabled;

                if (u.ParentAccountCategoryIdSet && u.ParentAccountCategoryId != row.ParentAccountCategoryId)
                    row.ParentAccountCategoryId = u.ParentAccountCategoryId;

                if (u.SortOrderSet && u.SortOrder != row.SortOrder)
                    row.SortOrder = u.SortOrder;
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }
    }
}