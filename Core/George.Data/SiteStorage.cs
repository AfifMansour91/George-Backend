using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class SiteStorage : StorageBase
    {
        public SiteStorage(GeorgeDBContext dbContext, ILogger<SiteStorage> logger)
            : base(dbContext, logger)
        {
        }
        public async Task<DataListResult<Site>> GetSitesAsync(
            SiteFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Site>();

            // Base sites query
            var query = _dbContext.Sites
                .Include(a => a.Account)
                .Include(s => s.BusinessTypes)
                .AsNoTracking();

            if (filter?.Search?.SearchTerm.HasValue() == true)
            {
                var term = filter.Search.SearchTerm!.Trim();

                query =
                    from a in query
                    where a.SiteName.Contains(term)
                    select a;
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.SiteName);

            // Add paging.
            query = query.Skip(paging.Skip).Take(paging.Take);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Site?> GetSiteAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.Sites
                .Include(a => a.Account)
                .Include(s => s.BusinessTypes)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == siteId, cancelToken);
        }

        public async Task<List<Site>> GetSitesByAccountAsync(int accountId, CancellationToken cancelToken)
        {
            return await _dbContext.Sites
                .Include(s => s.BusinessTypes)
                .Where(s => s.AccountId == accountId && !s.IsDeleted)
                .AsNoTracking()
                .OrderBy(s => s.SiteName)
                .ToListAsync(cancelToken);
        }

        public async Task<Site> CreateSiteAsync(Site site, List<int>? businessTypeIds, CancellationToken cancelToken)
        {
            _dbContext.Sites.Add(site);
            
            // Add business types if provided
            if (businessTypeIds != null && businessTypeIds.Any())
            {
                var businessTypes = await _dbContext.BusinessTypes
                    .Where(bt => businessTypeIds.Contains(bt.Id))
                    .ToListAsync(cancelToken);
                
                foreach (var bt in businessTypes)
                {
                    site.BusinessTypes.Add(bt);
                }
            }
            
            await _dbContext.SaveChangesAsync(cancelToken);
            return site;
        }

        public async Task<Site?> UpdateSiteAsync(Site updated, List<int>? businessTypeIds, CancellationToken cancelToken)
        {
            var dbSite = await _dbContext.Sites
                .Include(s => s.BusinessTypes)
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbSite == null) return null;

            dbSite.SiteName = updated.SiteName ?? dbSite.SiteName;
            dbSite.AccountId = updated.AccountId;
            dbSite.Location = updated.Location ?? dbSite.Location;
            dbSite.Description = updated.Description ?? dbSite.Description;
            dbSite.Status = updated.Status ?? dbSite.Status;
            dbSite.ContactEmail = updated.ContactEmail ?? dbSite.ContactEmail;
            dbSite.ContactPhone = updated.ContactPhone ?? dbSite.ContactPhone;
            dbSite.IsKosherSite = updated.IsKosherSite ?? dbSite.IsKosherSite;
            dbSite.AllowWeightedProducts = updated.AllowWeightedProducts ?? dbSite.AllowWeightedProducts;
            dbSite.Currency = updated.Currency ?? dbSite.Currency;
            dbSite.WooCommerceUrl = updated.WooCommerceUrl ?? dbSite.WooCommerceUrl;
            dbSite.WooCommerceKey = updated.WooCommerceKey ?? dbSite.WooCommerceKey;
            dbSite.WooCommerceSecret = updated.WooCommerceSecret ?? dbSite.WooCommerceSecret;
            dbSite.IsActive = updated.IsActive || dbSite.IsActive;
            dbSite.UpdatedDate = DateTime.UtcNow;

            // Update business types
            if (businessTypeIds != null)
            {
                dbSite.BusinessTypes.Clear();
                if (businessTypeIds.Any())
                {
                    var businessTypes = await _dbContext.BusinessTypes
                        .Where(bt => businessTypeIds.Contains(bt.Id))
                        .ToListAsync(cancelToken);
                    
                    foreach (var bt in businessTypes)
                    {
                        dbSite.BusinessTypes.Add(bt);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbSite;
        }

        public async Task<Site?> DeleteSiteAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Sites
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);
            if (dbModel != null)
            {
                // Delete the entity.
                _dbContext.Sites.Remove(dbModel);

                // Save to the DB.
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }

            return dbModel;
        }

        public async Task<Site?> ActivateSiteAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Sites
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);

            if (dbModel == null) return null;

            dbModel.IsActive = !dbModel.IsActive;
            dbModel.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbModel;
        }
    }
}

