using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    /// <summary>DB access for the delivery-provider abstraction (configs + dispatch history).</summary>
    public class DeliveryDispatchStorage : StorageBase
    {
        public DeliveryDispatchStorage(GeorgeDBContext dbContext, ILogger<DeliveryDispatchStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<List<DeliveryProviderConfig>> GetConfigsForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.DeliveryProviderConfig
                .AsNoTracking()
                .Where(c => !c.IsDeleted && c.SiteId == siteId)
                .OrderBy(c => c.ProviderKey)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
        }

        public async Task<DeliveryProviderConfig?> GetConfigAsync(int siteId, string providerKey, CancellationToken cancelToken)
        {
            return await _dbContext.DeliveryProviderConfig
                .AsNoTracking()
                .FirstOrDefaultAsync(c => !c.IsDeleted && c.SiteId == siteId && c.ProviderKey == providerKey, cancelToken)
                .ConfigureAwait(false);
        }

        /// <summary>Create-or-update the provider config row; mutate applies request fields onto the tracked entity.</summary>
        public async Task<DeliveryProviderConfig> UpsertConfigAsync(
            int siteId,
            string providerKey,
            Action<DeliveryProviderConfig> mutate,
            CancellationToken cancelToken)
        {
            var row = await _dbContext.DeliveryProviderConfig
                .FirstOrDefaultAsync(c => !c.IsDeleted && c.SiteId == siteId && c.ProviderKey == providerKey, cancelToken)
                .ConfigureAwait(false);
            if (row == null)
            {
                row = new DeliveryProviderConfig
                {
                    SiteId = siteId,
                    ProviderKey = providerKey,
                    CreationTime = DateTime.UtcNow,
                    // Webhook secret is minted once per config and survives later edits.
                    WebhookSecret = Guid.NewGuid().ToString("N"),
                };
                _dbContext.DeliveryProviderConfig.Add(row);
            }
            mutate(row);
            if (string.IsNullOrWhiteSpace(row.WebhookSecret))
                row.WebhookSecret = Guid.NewGuid().ToString("N");
            row.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return row;
        }

        public async Task<OrderDeliveryDispatch?> GetDispatchAsync(int orderId, string providerKey, CancellationToken cancelToken)
        {
            return await _dbContext.OrderDeliveryDispatch
                .FirstOrDefaultAsync(d => !d.IsDeleted && d.OrderId == orderId && d.ProviderKey == providerKey, cancelToken)
                .ConfigureAwait(false);
        }

        public async Task<List<OrderDeliveryDispatch>> GetDispatchesForOrderAsync(int orderId, CancellationToken cancelToken)
        {
            return await _dbContext.OrderDeliveryDispatch
                .AsNoTracking()
                .Where(d => !d.IsDeleted && d.OrderId == orderId)
                .OrderBy(d => d.ProviderKey)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
        }

        public async Task<OrderDeliveryDispatch?> GetDispatchByTaskAsync(string providerKey, string externalTaskId, CancellationToken cancelToken)
        {
            return await _dbContext.OrderDeliveryDispatch
                .FirstOrDefaultAsync(
                    d => !d.IsDeleted && d.ProviderKey == providerKey && d.ExternalTaskId == externalTaskId,
                    cancelToken)
                .ConfigureAwait(false);
        }

        /// <summary>Create-or-update the single dispatch row of (order, provider); mutate applies the attempt result.</summary>
        public async Task<OrderDeliveryDispatch> UpsertDispatchAsync(
            int orderId,
            int siteId,
            string providerKey,
            Action<OrderDeliveryDispatch> mutate,
            CancellationToken cancelToken)
        {
            var row = await _dbContext.OrderDeliveryDispatch
                .FirstOrDefaultAsync(d => !d.IsDeleted && d.OrderId == orderId && d.ProviderKey == providerKey, cancelToken)
                .ConfigureAwait(false);
            if (row == null)
            {
                row = new OrderDeliveryDispatch
                {
                    OrderId = orderId,
                    SiteId = siteId,
                    ProviderKey = providerKey,
                    Status = "pending",
                    CreationTime = DateTime.UtcNow,
                };
                _dbContext.OrderDeliveryDispatch.Add(row);
            }
            mutate(row);
            row.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return row;
        }
    }
}
