using George.Common;
using George.Common.Request;
using George.Data.Models;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class BusinessTypeStorage : StorageBase
    {
        public BusinessTypeStorage(GeorgeDBContext dbContext, ILogger<BusinessTypeStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<BusinessType>> GetBusinessTypesAsync(
            BusinessTypeFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken = default)
        {
            DataListResult<BusinessType> res = new DataListResult<BusinessType>();

            // Build the query.
            var query = _dbContext.BusinessTypes.AsNoTracking();

            // Filter.
            if (filter.Name.HasValue())
                query = query.Where(a => a.Name != null && a.Name.Contains(filter.Name));
            if (filter.Description != null)
                query = query.Where(a => a.Description != null && filter.Description.Contains(a.Description));
            if (filter.Icon != null)
                query = query.Where(a => a.Icon != null && filter.Icon.Contains(a.Icon));
            //if (filter.StatusId.HasValue)
            //    query = query.Where(a => a.StatusId == (int)filter.StatusId);
            if (filter.Search != null && filter.Search.SearchTerm.HasValue())
            {
                var term = filter.Search.SearchTerm!;
                query = query.Where(a =>
                    a.Name.Contains(term) ||
                    a.Description.Contains(term) ||
                    a.Icon.Contains(term)
                    );
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.Name);

            // Add paging.
            //query = query.Skip(paging.Skip).Take(paging.Take);

            //// Add includes.
            //query = query.Include(a => a.Organization)
            //                //.Include(a => a.AccountSubscriptions.FirstOrDefault(a => a.IsActive)).ThenInclude(b => b.Subscription)
            //                .Include(a => a.AccountSubscriptions.Where(a => a.IsActive)).ThenInclude(b => b.Subscription)
            //                .Include(a => a.Owner)
            //                .Include(a => a.AccountUsers.Where(b => b.UserId == userId)).ThenInclude(c => c.User);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<BusinessType?> GetBusinessTypeAsync(long businessTypeId, CancellationToken cancelToken)
        {
            return await _dbContext.BusinessTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == businessTypeId, cancelToken);
        }

        public async Task<BusinessType> CreateBusinessTypeAsync(BusinessType businessType, CancellationToken cancelToken)
        {
            _dbContext.BusinessTypes.Add(businessType);
            await _dbContext.SaveChangesAsync(cancelToken);
            return businessType;
        }

        public async Task<BusinessType?> UpdateBusinessTypeAsync(BusinessType updated, CancellationToken cancelToken)
        {
            var dbAcc = await _dbContext.BusinessTypes
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAcc == null) return null;

            dbAcc.Name = updated.Name;
            dbAcc.Description = updated.Description;
            dbAcc.Icon = updated.Icon;
            //dbAcc.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAcc;
        }

        public async Task<BusinessType?> DeleteBusinessTypeAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.BusinessTypes
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);
            if (dbModel != null)
            {
                // Delete the entity.
                _dbContext.BusinessTypes.Remove(dbModel);

                // Save to the DB.
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }

            return dbModel;
        }

    }
}

