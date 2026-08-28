using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    /// <summary>DB reads for דוח הזמנות (operational orders/deliveries report).</summary>
    public class OrdersReportStorage : StorageBase
    {
        public OrdersReportStorage(GeorgeDBContext dbContext, ILogger<OrdersReportStorage> logger)
            : base(dbContext, logger)
        {
        }

        /// <summary>
        /// Non-cancelled orders whose date is in the inclusive calendar range <paramref name="fromLocalDate"/>..<paramref name="toLocalDate"/>.
        /// Date basis: supply = effective delivery date (DeliveryDate, else PickupDate, else CreationTime — same
        /// semantics as the orders-archive filter); order = CreationTime.
        /// </summary>
        public async Task<List<Order>> GetOrdersForReportAsync(
            int siteId,
            DateTime fromLocalDate,
            DateTime toLocalDate,
            bool byOrderDate,
            CancellationToken cancelToken)
        {
            var fromD = fromLocalDate.Date;
            var toD = toLocalDate.Date;

            var query = _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.SiteId == siteId)
                // Exact literal — EF cannot translate string.Equals(..., StringComparison).
                .Where(o => o.Status != "Cancelled");

            query = byOrderDate
                ? query.Where(o => o.CreationTime.Date >= fromD && o.CreationTime.Date <= toD)
                : query.Where(o =>
                    (o.DeliveryDate ?? o.PickupDate ?? o.CreationTime).Date >= fromD &&
                    (o.DeliveryDate ?? o.PickupDate ?? o.CreationTime).Date <= toD);

            return await query.ToListAsync(cancelToken).ConfigureAwait(false);
        }
    }
}
