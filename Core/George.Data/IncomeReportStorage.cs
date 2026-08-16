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
        /// Optional coupon: substring on <see cref="Order.CouponCode"/> first, then legacy text/JSON fields for older rows.
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
                    (o.CouponCode != null && o.CouponCode.Contains(c)) ||
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
                // Needed by ProductCatalogStockClassification.IsWeightedLikeProduct (kg vs units labels).
                .Include(p => p.SetupType)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            return products.ToDictionary(p => p.Id);
        }

        /// <summary>
        /// Latest completed/delivered order time per catalog product (any history).
        /// Matches <see cref="OrderItem.ProductId"/> and legacy <see cref="OrderItem.WooCommerceProductId"/> lines.
        /// Includes completed COD (<c>Unpaid</c>/<c>Pending</c>) so stock KPI footnotes are not blank when only paid lines are in the income window.
        /// </summary>
        public async Task<Dictionary<int, DateTime>> GetLastPaidCompletedOrderCreationTimeUtcPerProductAsync(
            int siteId,
            IEnumerable<(int ProductId, int? WooCommerceId)> products,
            CancellationToken cancelToken)
        {
            var idSet = products.Select(p => p.ProductId).Where(id => id > 0).Distinct().ToArray();
            var wooToProduct = products
                .Where(p => p.ProductId > 0 && p.WooCommerceId is > 0)
                .GroupBy(p => p.WooCommerceId!.Value)
                .ToDictionary(g => g.Key, g => g.First().ProductId);
            var wooIdSet = wooToProduct.Keys.ToArray();
            if (idSet.Length == 0 && wooIdSet.Length == 0)
                return new Dictionary<int, DateTime>();

            var lines = await (
                from i in _dbContext.OrderItem.AsNoTracking()
                join o in _dbContext.Order.AsNoTracking() on i.OrderId equals o.Id
                where !i.IsDeleted && !o.IsDeleted
                      && o.SiteId == siteId
                      && (o.Status == "Completed" || o.Status == "Delivered" || o.Status == "Ready")
                      && (o.PaymentStatus == null
                          || o.PaymentStatus == ""
                          || ((o.PaymentStatus.ToLower() == "paid"
                               || o.PaymentStatus.ToLower() == "unpaid"
                               || o.PaymentStatus.ToLower() == "pending")
                              && o.PaymentStatus.ToLower() != "refunded"
                              && o.PaymentStatus.ToLower() != "failed"))
                      && ((i.ProductId != null && idSet.Contains(i.ProductId.Value))
                          || (i.WooCommerceProductId != null && wooIdSet.Contains(i.WooCommerceProductId.Value)))
                select new { i.ProductId, i.WooCommerceProductId, o.CreationTime }
            ).ToListAsync(cancelToken).ConfigureAwait(false);

            var map = new Dictionary<int, DateTime>();
            foreach (var row in lines)
            {
                var pid = row.ProductId;
                if (pid is not > 0
                    && row.WooCommerceProductId is > 0
                    && wooToProduct.TryGetValue(row.WooCommerceProductId.Value, out var mapped))
                    pid = mapped;
                if (pid is not > 0) continue;

                var ct = row.CreationTime;
                if (ct.Kind == DateTimeKind.Local)
                    ct = ct.ToUniversalTime();
                else if (ct.Kind == DateTimeKind.Unspecified)
                    ct = DateTime.SpecifyKind(ct, DateTimeKind.Utc);

                if (!map.TryGetValue(pid.Value, out var prev) || ct > prev)
                    map[pid.Value] = ct;
            }

            return map;
        }
    }
}
