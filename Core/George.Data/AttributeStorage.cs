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
            AttributeFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken = default)
        {
            DataListResult<Attribute> res = new DataListResult<Attribute>();

            // Build the query.
            var query = _dbContext.Attribute
                .Include(a => a.Site)
                .Include(a => a.AttributeValue)
                .AsNoTracking();

            // Filter.
            if (filter != null)
            {
                if (filter.Name.HasValue())
                    query = query.Where(a => a.Name.Contains(filter.Name));

                if (filter.SiteIds != null && filter.SiteIds.Any())
                {
                    query = query.Where(a => filter.SiteIds.Contains(a.SiteId));
                }

                if (filter.Search != null && filter.Search.SearchTerm.HasValue())
                {
                    var term = filter.Search.SearchTerm!;
                    query = query.Where(a => a.Name.Contains(term));
                }
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.Name);

            // Add paging.
            //query = query.Skip(paging.Skip).Take(paging.Take);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Attribute?> GetAttributeAsync(int attributeId, CancellationToken cancelToken)
        {
            return await _dbContext.Attribute
                .Include(a => a.Site)
                .Include(a => a.AttributeValue)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attributeId, cancelToken);
        }

        public async Task<Attribute> CreateAttributeAsync(Attribute attribute, List<string>? values, CancellationToken cancelToken)
        {
            _dbContext.Attribute.Add(attribute);
            await _dbContext.SaveChangesAsync(cancelToken);

            // Add attribute values if provided
            if (values != null && values.Any())
            {
                foreach (var value in values)
                {
                    _dbContext.AttributeValue.Add(new AttributeValue
                    {
                        AttributeId = attribute.Id,
                        Value = value
                    });
                }
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return attribute;
        }

        public async Task<Attribute?> UpdateAttributeAsync(Attribute updated, List<string>? values, CancellationToken cancelToken)
        {
            var dbAttr = await _dbContext.Attribute
                .Include(a => a.AttributeValue)
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAttr == null) return null;

            dbAttr.Name = updated.Name;
            dbAttr.SiteId = updated.SiteId | dbAttr.SiteId;
            dbAttr.UpdatedDate = DateTime.UtcNow;
            dbAttr.UpdateUserId = updated.UpdateUserId;

            // Update attribute values
            if (values != null)
            {
                // Remove existing values
                _dbContext.AttributeValue.RemoveRange(dbAttr.AttributeValue);
                
                // Add new values
                if (values.Any())
                {
                    foreach (var value in values)
                    {
                        _dbContext.AttributeValue.Add(new AttributeValue
                        {
                            AttributeId = dbAttr.Id,
                            Value = value
                        });
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAttr;
        }

        public async Task<bool> UpdateAttributeWooCommerceIdAsync(int attributeId, int? wooCommerceId, CancellationToken cancelToken)
        {
            var attribute = await _dbContext.Attribute
                .FirstOrDefaultAsync(a => a.Id == attributeId, cancelToken);

            if (attribute == null) return false;

            attribute.WooCommerceId = wooCommerceId;
            attribute.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> DeleteAttributeAsync(int id, CancellationToken cancelToken = default)
        {
            var dbModel = await _dbContext.Attribute
                .FirstOrDefaultAsync(a => a.Id == id, cancelToken);

            if (dbModel == null) return false;

            dbModel.IsDeleted = true;
            dbModel.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

    }
}

