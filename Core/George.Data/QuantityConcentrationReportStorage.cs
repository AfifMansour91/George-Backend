using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    /// <summary>DB reads for דוח ריכוז כמויות (open orders by delivery date).</summary>
    public class QuantityConcentrationReportStorage : StorageBase
    {
        public QuantityConcentrationReportStorage(GeorgeDBContext dbContext, ILogger<QuantityConcentrationReportStorage> logger)
            : base(dbContext, logger)
        {
        }

        /// <summary>
        /// Orders that are not <see cref="Order.Status"/> Delivered/Cancelled, with effective delivery date
        /// (DeliveryDate, else PickupDate, else CreationTime) in the inclusive calendar range <paramref name="fromLocalDate"/>..<paramref name="toLocalDate"/>.
        /// </summary>
        public async Task<List<Order>> GetOpenOrdersForDeliveryDateRangeAsync(
            int siteId,
            DateTime fromLocalDate,
            DateTime toLocalDate,
            CancellationToken cancelToken)
        {
            var fromD = fromLocalDate.Date;
            var toD = toLocalDate.Date;

            var list = await _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.SiteId == siteId)
                // Exact literals — EF cannot translate string.Equals(..., StringComparison).
                .Where(o => o.Status != "Delivered" && o.Status != "Cancelled")
                .Include(o => o.OrderItem)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            var filtered = new List<Order>();
            foreach (var o in list)
            {
                var eff = (o.DeliveryDate ?? o.PickupDate ?? o.CreationTime).Date;
                if (eff < fromD || eff > toD)
                    continue;

                if (o.OrderItem != null)
                {
                    var active = o.OrderItem.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).ToList();
                    o.OrderItem = active;
                }

                filtered.Add(o);
            }

            return filtered;
        }
    }
}
