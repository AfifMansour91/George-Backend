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
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == siteId, cancelToken);
        }

        public async Task<Site> CreateSiteAsync(Site site, CancellationToken cancelToken)
        {
            _dbContext.Sites.Add(site);
            await _dbContext.SaveChangesAsync(cancelToken);
            return site;
        }

        public async Task<Site?> UpdateSiteAsync(Site updated, CancellationToken cancelToken)
        {
            var dbAcc = await _dbContext.Sites
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAcc == null) return null;

            dbAcc.SiteName = updated.SiteName;
            dbAcc.AccountId = updated.AccountId;
            dbAcc.Location = updated.Location;
            dbAcc.Description = updated.Description;
            dbAcc.Status = updated.Status;
            dbAcc.ContactEmail = updated.ContactEmail;
            dbAcc.ContactPhone = updated.ContactPhone;
            dbAcc.IsKosherSite = updated.IsKosherSite;
            dbAcc.AllowWeightedProducts = updated.AllowWeightedProducts;
            dbAcc.IsActive = updated.IsActive;
            dbAcc.UpdatedDate = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAcc;
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

