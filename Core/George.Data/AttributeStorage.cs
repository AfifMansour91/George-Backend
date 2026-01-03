using George.Common;
using George.Common.Request;
using George.Data.Models;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Attribute = George.DB.Attribute;

namespace George.Data
{
    public class AttributeStorage : StorageBase
    {
        public AttributeStorage(GeorgeDBContext dbContext, ILogger<AttributeStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Attribute>> GetAttributesAsync(
            AttributeFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken = default)
        {
            DataListResult<Attribute> res = new DataListResult<Attribute>();

            // Build the query.
            var query = _dbContext.Attributes.AsNoTracking();

            // Filter.
            if (filter.Name.HasValue())
                query = query.Where(a => a.Name != null && a.Name.Contains(filter.Name));
            //if (filter.Value != null)
            //    query = query.Where(a => a.AttributeValues != null && filter.Value.Contains(a.Value));
            //if (filter.StatusId.HasValue)
            //    query = query.Where(a => a.StatusId == (int)filter.StatusId);
            if (filter.Search != null && filter.Search.SearchTerm.HasValue())
            {
                var term = filter.Search.SearchTerm!;
                query = query.Where(a =>
                    a.Name.Contains(term));
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.Name);

            // Add paging.
            query = query.Skip(paging.Skip).Take(paging.Take);

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

        public async Task<Attribute?> GetAttributeAsync(long attributeId, CancellationToken cancelToken)
        {
            return await _dbContext.Attributes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attributeId, cancelToken);
        }

        public async Task<Attribute> CreateAttributeAsync(Attribute attribute, CancellationToken cancelToken)
        {
            _dbContext.Attributes.Add(attribute);
            await _dbContext.SaveChangesAsync(cancelToken);
            return attribute;
        }

        public async Task<Attribute?> UpdateAttributeAsync(Attribute updated, CancellationToken cancelToken)
        {
            var dbAcc = await _dbContext.Attributes
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAcc == null) return null;

            dbAcc.Name = updated.Name;
            //dbAcc.Description = updated.Description;
            //dbAcc.Icon = updated.Icon;
            //dbAcc.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAcc;
        }

        public async Task<Attribute?> DeleteAttributeAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Attributes
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);
            if (dbModel != null)
            {
                // Delete the entity.
                _dbContext.Attributes.Remove(dbModel);

                // Save to the DB.
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }

            return dbModel;
        }

    }
}

