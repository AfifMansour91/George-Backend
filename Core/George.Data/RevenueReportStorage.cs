using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class RevenueReportStorage : StorageBase
    {
        public const string CityEmptyFilterKey = "__no_city__";

        public RevenueReportStorage(GeorgeDBContext dbContext, ILogger<RevenueReportStorage> logger)
            : base(dbContext, logger)
        {
        }

        /// <summary>
        /// Orders in the report window.
        /// By order: <see cref="Order.CreationTime"/> in range (all statuses).
        /// By charge: <see cref="Order.PaidAt"/> in range, legacy paid rows, successful charge events,
        /// or cancelled-before-charge rows by <see cref="Order.UpdatedDate"/> (cancellation time).
        /// </summary>
        public async Task<List<Order>> GetOrdersInWindowAsync(
            int siteId,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            bool byChargeDate,
            CancellationToken cancelToken)
        {
            var q = _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.SiteId == siteId);

            if (byChargeDate)
            {
                var chargedOrderIds = await (
                    from e in _dbContext.OrderPaymentEvent.AsNoTracking()
                    join o in _dbContext.Order.AsNoTracking() on e.OrderId equals o.Id
                    where !o.IsDeleted
                          && o.SiteId == siteId
                          && e.CreationTime >= fromUtc
                          && e.CreationTime < toUtcExclusive
                          && (e.StatusCode == "0" || e.StatusCode == "000" || e.StatusCode == "Success")
                          && (e.EventType == "ChargeToken" || e.EventType == "CaptureAuthorization")
                    select o.Id
                ).Distinct().ToListAsync(cancelToken).ConfigureAwait(false);

                q = q.Where(o =>
                    (o.PaidAt != null && o.PaidAt >= fromUtc && o.PaidAt < toUtcExclusive) ||
                    (o.PaidAt == null
                        && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Captured")
                        && o.UpdatedDate != null
                        && o.UpdatedDate >= fromUtc
                        && o.UpdatedDate < toUtcExclusive) ||
                    chargedOrderIds.Contains(o.Id) ||
                    (o.Status == "Cancelled"
                        && o.PaidAt == null
                        && o.PaymentStatus != "Paid"
                        && o.PaymentStatus != "Refunded"
                        && o.UpdatedDate != null
                        && o.UpdatedDate >= fromUtc
                        && o.UpdatedDate < toUtcExclusive));
            }
            else
            {
                q = q.Where(o => o.CreationTime >= fromUtc && o.CreationTime < toUtcExclusive);
            }

            var list = await q
                .Include(o => o.OrderItem)
                .OrderByDescending(o => o.CreationTime)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            foreach (var o in list)
            {
                if (o.OrderItem == null) continue;
                o.OrderItem = o.OrderItem.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).ToList();
            }

            return list;
        }

        /// <summary>
        /// Orders not yet charged (pipeline): active, not cancelled, payment unsettled.
        /// No filter on delivery date — any open unpaid order counts.
        /// </summary>
        public async Task<List<Order>> GetPipelineOrdersAsync(int siteId, CancellationToken cancelToken)
        {
            return await _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted
                    && o.SiteId == siteId
                    && o.Status != "Cancelled"
                    && o.PaymentStatus != "Paid"
                    && o.PaymentStatus != "Refunded")
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
        }

        public async Task<Dictionary<int, Product>> GetProductsWithCategoriesAsync(
            IEnumerable<int> productIds,
            CancellationToken cancelToken)
        {
            var ids = productIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, Product>();

            var products = await _dbContext.Product
                .AsNoTracking()
                .Where(p => ids.Contains(p.Id))
                .Include(p => p.ProductCategory)
                .ThenInclude(pc => pc.Category)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            return products.ToDictionary(p => p.Id);
        }

        /// <summary>Sum of successful refund event amounts per order (for revenue credits KPI).</summary>
        public async Task<Dictionary<int, decimal>> GetSuccessfulRefundTotalsByOrderIdsAsync(
            IEnumerable<int> orderIds,
            CancellationToken cancelToken)
        {
            var ids = orderIds.Where(id => id > 0).Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<int, decimal>();

            var rows = await _dbContext.OrderPaymentEvent
                .AsNoTracking()
                .Where(e => ids.Contains(e.OrderId)
                    && e.EventType == "Refund"
                    && (e.StatusCode == "0" || e.StatusCode == "000" || e.StatusCode == "Success"))
                .GroupBy(e => e.OrderId)
                .Select(g => new { OrderId = g.Key, Total = g.Sum(e => e.Amount ?? 0m) })
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            return rows.ToDictionary(x => x.OrderId, x => x.Total);
        }
    }
}
