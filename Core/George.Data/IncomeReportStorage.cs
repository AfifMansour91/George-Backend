using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class IncomeReportStorage : StorageBase
    {
        public IncomeReportStorage(GeorgeDBContext dbContext, ILogger<IncomeReportStorage> logger)
            : base(dbContext, logger)
        {
        }

        /// <summary>
        /// Completed + paid orders in [fromUtc, toUtcExclusive) for income reporting.
        /// Optional coupon: substring match on billing / WC JSON / notes (until dedicated coupon column exists).
        /// </summary>
        public async Task<List<Order>> GetReportOrdersAsync(
            int siteId,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            string? couponContains,
            CancellationToken cancelToken)
        {
            var q = _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted
                    && o.SiteId == siteId
                    && (o.Status == "Completed" || o.Status == "Delivered" || o.Status == "Ready")
                    && o.PaymentStatus == "Paid"
                    && o.CreationTime >= fromUtc
                    && o.CreationTime < toUtcExclusive);

            if (!string.IsNullOrWhiteSpace(couponContains))
            {
                var c = couponContains.Trim();
                q = q.Where(o =>
                    (o.BillingNotes != null && o.BillingNotes.Contains(c)) ||
                    (o.WooCommerceRequestJson != null && o.WooCommerceRequestJson.Contains(c)) ||
                    (o.CustomerNote != null && o.CustomerNote.Contains(c)) ||
                    (o.ManagerNote != null && o.ManagerNote.Contains(c)) ||
                    (o.InternalOrderNotes != null && o.InternalOrderNotes.Contains(c)) ||
                    (o.DeliveryNote != null && o.DeliveryNote.Contains(c)) ||
                    (o.ShippingInfoJson != null && o.ShippingInfoJson.Contains(c)));
            }

            var list = await q
                .Include(o => o.OrderItem)
                .OrderByDescending(o => o.CreationTime)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            foreach (var o in list)
            {
                if (o.OrderItem == null) continue;
                var active = o.OrderItem.Where(i => !i.IsDeleted).OrderBy(i => i.SortOrder).ToList();
                o.OrderItem = active;
            }

            return list;
        }

        public async Task<HashSet<int>> GetCustomerIdsWithPriorPaidCompletedOrdersAsync(
            int siteId,
            DateTime beforeUtcExclusive,
            CancellationToken cancelToken)
        {
            var list = await _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted
                    && o.SiteId == siteId
                    && (o.Status == "Completed" || o.Status == "Delivered" || o.Status == "Ready")
                    && o.PaymentStatus == "Paid"
                    && o.CreationTime < beforeUtcExclusive
                    && o.CustomerId != null)
                .Select(o => o.CustomerId!.Value)
                .Distinct()
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
            return list.ToHashSet();
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
                .Include(p => p.ProductImage)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            return products.ToDictionary(p => p.Id);
        }
    }
}
