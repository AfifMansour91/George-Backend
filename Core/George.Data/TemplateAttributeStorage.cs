using George.Common;
using George.Common.Request;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class TemplateAttributeStorage : StorageBase
    {
        public TemplateAttributeStorage(GeorgeDBContext dbContext, ILogger<TemplateAttributeStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<TemplateAttribute>> GetTemplateAttributesAsync(
            TemplateAttributeFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<TemplateAttribute>();

            var query = _dbContext.TemplateAttribute
                .Include(ta => ta.TemplateAttributeValue)
                .Include(ta => ta.Site)
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.SiteId.HasValue)
                {
                    query = query.Where(ta => ta.Site.Any(s => s.Id == filter.SiteId.Value) || !ta.Site.Any());
                }

                if (filter.SiteIds != null && filter.SiteIds.Any())
                {
                    query = query.Where(ta => ta.Site.Any(s => filter.SiteIds.Contains(s.Id)) || !ta.Site.Any());
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(ta => ta.Name.Contains(term));
                }
            }

            // Only get non-deleted template attributes
            query = query.Where(ta => !ta.IsDeleted);

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query.OrderBy(ta => ta.Name);

            //query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<TemplateAttribute?> GetTemplateAttributeAsync(int templateAttributeId, CancellationToken cancelToken)
        {
            return await _dbContext.TemplateAttribute
                .Include(ta => ta.TemplateAttributeValue)
                .Include(ta => ta.Site)
                .AsNoTracking()
                .FirstOrDefaultAsync(ta => ta.Id == templateAttributeId && !ta.IsDeleted, cancelToken);
        }

        public async Task<TemplateAttribute> CreateTemplateAttributeAsync(
            TemplateAttribute templateAttribute,
            List<string>? values,
            List<int>? siteIds,
            CancellationToken cancelToken)
        {
            _dbContext.TemplateAttribute.Add(templateAttribute);
            await _dbContext.SaveChangesAsync(cancelToken);

            // Add values if provided
            if (values != null && values.Any())
            {
                foreach (var value in values)
                {
                    _dbContext.TemplateAttributeValue.Add(new TemplateAttributeValue
                    {
                        TemplateAttributeId = templateAttribute.Id,
                        Value = value
                    });
                }
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            // Add sites if provided
            if (siteIds != null && siteIds.Any())
            {
                var sites = await _dbContext.Site
                    .Where(s => siteIds.Contains(s.Id))
                    .ToListAsync(cancelToken);
                
                foreach (var site in sites)
                {
                    templateAttribute.Site.Add(site);
                }
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return templateAttribute;
        }

        public async Task<TemplateAttribute?> UpdateTemplateAttributeAsync(
            TemplateAttribute updated,
            List<string>? values,
            List<int>? siteIds,
            CancellationToken cancelToken)
        {
            var dbTemplateAttribute = await _dbContext.TemplateAttribute
                .Include(ta => ta.TemplateAttributeValue)
                .Include(ta => ta.Site)
                .FirstOrDefaultAsync(ta => ta.Id == updated.Id && !ta.IsDeleted, cancelToken);

            if (dbTemplateAttribute == null) return null;

            // Update basic properties
            dbTemplateAttribute.Name = updated.Name;
            dbTemplateAttribute.UpdatedDate = DateTime.UtcNow;
            dbTemplateAttribute.UpdateUserId = updated.UpdateUserId;

            // Update values
            if (values != null)
            {
                // Remove existing values
                _dbContext.TemplateAttributeValue.RemoveRange(dbTemplateAttribute.TemplateAttributeValue);
                
                // Add new values
                if (values.Any())
                {
                    foreach (var value in values)
                    {
                        _dbContext.TemplateAttributeValue.Add(new TemplateAttributeValue
                        {
                            TemplateAttributeId = dbTemplateAttribute.Id,
                            Value = value
                        });
                    }
                }
            }

            // Update sites
            if (siteIds != null)
            {
                dbTemplateAttribute.Site.Clear();
                if (siteIds.Any())
                {
                    var sites = await _dbContext.Site
                        .Where(s => siteIds.Contains(s.Id))
                        .ToListAsync(cancelToken);
                    
                    foreach (var site in sites)
                    {
                        dbTemplateAttribute.Site.Add(site);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbTemplateAttribute;
        }

        public async Task<bool> DeleteTemplateAttributeAsync(int templateAttributeId, CancellationToken cancelToken)
        {
            var templateAttribute = await _dbContext.TemplateAttribute
                .FirstOrDefaultAsync(ta => ta.Id == templateAttributeId && !ta.IsDeleted, cancelToken);

            if (templateAttribute == null) return false;

            templateAttribute.IsDeleted = true;
            templateAttribute.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }
    }
}

