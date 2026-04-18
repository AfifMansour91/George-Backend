using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class ProductsReportStorage : StorageBase
    {
        public ProductsReportStorage(GeorgeDBContext dbContext, ILogger<ProductsReportStorage> logger)
            : base(dbContext, logger)
        {
        }

        /// <summary>Active catalog products linked to the site (for counts, stock KPIs, images).</summary>
        public async Task<List<Product>> GetSiteCatalogProductsAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.Product
                .AsNoTracking()
                .Where(p => !p.IsDeleted && p.IsActive && p.Site.Any(s => s.Id == siteId))
                .Include(p => p.StockStatus)
                .Include(p => p.StockManagementType)
                .Include(p => p.SetupType)
                .Include(p => p.WeightConfig)
                    .ThenInclude(wc => wc!.UnitWeightMode)
                .Include(p => p.ProductVariant)
                .Include(p => p.ProductCategory)
                .ThenInclude(pc => pc.Category)
                .Include(p => p.ProductImage)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
        }

        public async Task<Account?> GetAccountForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var accountId = await _dbContext.Site
                .AsNoTracking()
                .Where(s => !s.IsDeleted && s.Id == siteId)
                .Select(s => (int?)s.AccountId)
                .FirstOrDefaultAsync(cancelToken)
                .ConfigureAwait(false);
            if (accountId is null or <= 0)
                return null;

            return await _dbContext.Account
                .AsNoTracking()
                .Where(a => !a.IsDeleted && a.Id == accountId)
                .FirstOrDefaultAsync(cancelToken)
                .ConfigureAwait(false);
        }
    }
}
