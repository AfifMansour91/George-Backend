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

            // Base sites query (only non-deleted business types)
            var query = _dbContext.Site
                .Include(a => a.Account)
                .Include(s => s.BusinessType.Where(bt => !bt.IsDeleted))
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
            //query = query.Skip(paging.Skip).Take(paging.Take);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Site?> GetSiteAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.Site
                .Include(a => a.Account)
                .Include(s => s.BusinessType)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == siteId, cancelToken);
        }

        public async Task<List<Site>> GetSitesByAccountAsync(int accountId, CancellationToken cancelToken)
        {
            return await _dbContext.Site
                .Include(s => s.BusinessType.Where(bt => !bt.IsDeleted))
                .Where(s => s.AccountId == accountId && !s.IsDeleted)
                .AsNoTracking()
                .OrderBy(s => s.SiteName)
                .ToListAsync(cancelToken);
        }

        public async Task<Site> CreateSiteAsync(Site site, List<int>? businessTypeIds, CancellationToken cancelToken)
        {
            _dbContext.Site.Add(site);
            
            // Add business types if provided (only non-deleted)
            if (businessTypeIds != null && businessTypeIds.Any())
            {
                var businessTypes = await _dbContext.BusinessType
                    .Where(bt => businessTypeIds.Contains(bt.Id) && !bt.IsDeleted)
                    .ToListAsync(cancelToken);
                
                foreach (var bt in businessTypes)
                {
                    site.BusinessType.Add(bt);
                }
            }
            
            await _dbContext.SaveChangesAsync(cancelToken);
            return site;
        }

        public async Task<Site?> UpdateSiteAsync(Site updated, List<int>? businessTypeIds, CancellationToken cancelToken)
        {
            var dbSite = await _dbContext.Site
                .Include(s => s.BusinessType.Where(bt => !bt.IsDeleted))
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
            if (updated.WooCommerceEnabled.HasValue)
            {
                dbSite.WooCommerceEnabled = updated.WooCommerceEnabled;
            }
            // Shop settings (Sprint 2)
            if (updated.WeightTolerancePercent.HasValue) dbSite.WeightTolerancePercent = updated.WeightTolerancePercent;
            if (updated.DepreciationEnabled.HasValue) dbSite.DepreciationEnabled = updated.DepreciationEnabled;
            if (updated.DepreciationPercentagesJson != null) dbSite.DepreciationPercentagesJson = updated.DepreciationPercentagesJson;
            if (updated.PrepTimeMinutes.HasValue) dbSite.PrepTimeMinutes = updated.PrepTimeMinutes;
            if (updated.ShippingCost.HasValue) dbSite.ShippingCost = updated.ShippingCost;
            if (updated.FreeShippingAbove.HasValue) dbSite.FreeShippingAbove = updated.FreeShippingAbove;
            if (updated.AutoPrintEnabled.HasValue) dbSite.AutoPrintEnabled = updated.AutoPrintEnabled;
            if (updated.PrintNewOrderImmediate.HasValue) dbSite.PrintNewOrderImmediate = updated.PrintNewOrderImmediate;
            if (updated.PrintMovedToTreatment.HasValue) dbSite.PrintMovedToTreatment = updated.PrintMovedToTreatment;
            if (updated.PrintFutureImmediate.HasValue) dbSite.PrintFutureImmediate = updated.PrintFutureImmediate;
            if (updated.PrintFutureAtTimeEnabled.HasValue) dbSite.PrintFutureAtTimeEnabled = updated.PrintFutureAtTimeEnabled;
            if (updated.PrintFutureAtTime != null) dbSite.PrintFutureAtTime = updated.PrintFutureAtTime;
            if (updated.VoucherPrinterSilent.HasValue) dbSite.VoucherPrinterSilent = updated.VoucherPrinterSilent;
            if (updated.VoucherPrinterName != null) dbSite.VoucherPrinterName = updated.VoucherPrinterName;
            dbSite.IsActive = updated.IsActive || dbSite.IsActive;
            dbSite.UpdatedDate = DateTime.UtcNow;

            // Update business types
            if (businessTypeIds != null)
            {
                dbSite.BusinessType.Clear();
                if (businessTypeIds.Any())
                {
                    var businessTypes = await _dbContext.BusinessType
                        .Where(bt => businessTypeIds.Contains(bt.Id) && !bt.IsDeleted)
                        .ToListAsync(cancelToken);
                    
                    foreach (var bt in businessTypes)
                    {
                        dbSite.BusinessType.Add(bt);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbSite;
        }

        public async Task<Site?> DeleteSiteAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Site
                                .Where(a => a.Id == id)
                                .FirstOrDefaultAsync(cancelToken)
                                .ConfigureAwait(false);
            if (dbModel != null)
            {
                // Delete the entity.
                _dbContext.Site.Remove(dbModel);

                // Save to the DB.
                await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            }

            return dbModel;
        }

        public async Task<Site?> ActivateSiteAsync(int id, CancellationToken cancelToken = default)
        {
            // Get the data from the DB.
            var dbModel = await _dbContext.Site
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

