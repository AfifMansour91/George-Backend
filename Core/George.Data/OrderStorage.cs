using George.Common;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class OrderStorage : StorageBase
    {
        public OrderStorage(GeorgeDBContext dbContext, ILogger<OrderStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Order>> GetOrdersAsync(
            OrderFilter filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Order>();
            var query = _dbContext.Orders
                .Where(o => !o.IsDeleted)
                .Include(o => o.OrderItems.OrderBy(i => i.SortOrder))
                .AsNoTracking();

            if (filter?.SiteId.HasValue == true)
                query = query.Where(o => o.SiteId == filter.SiteId!.Value);

            if (filter?.Status.HasValue() == true)
                query = query.Where(o => o.Status == filter.Status!.Trim());

            if (filter?.Source.HasValue() == true)
                query = query.Where(o => o.Source == filter.Source!.Trim());

            if (filter?.DeliveryType.HasValue() == true)
                query = query.Where(o => o.DeliveryType == filter.DeliveryType!.Trim());

            if (filter?.PaymentStatus.HasValue() == true)
                query = query.Where(o => o.PaymentStatus == filter.PaymentStatus!.Trim());

            //if (filter?.DeliveryDateFrom.HasValue == true)
            //    query = query.Where(o => o.DeliveryDate >= filter.DeliveryDateFrom);

            //if (filter?.DeliveryDateTo.HasValue == true)
            //    query = query.Where(o => o.DeliveryDate <= filter.DeliveryDateTo);

            if (filter?.Search?.SearchTerm.HasValue() == true)
            {
                var term = filter.Search.SearchTerm!.Trim();
                query = query.Where(o =>
                    (o.OrderNumber != null && o.OrderNumber.Contains(term)) ||
                    (o.CustomerName != null && o.CustomerName.Contains(term)) ||
                    (o.CustomerPhone != null && o.CustomerPhone.Contains(term)) ||
                    (o.CustomerNote != null && o.CustomerNote.Contains(term)));
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query.OrderByDescending(o => o.CreationTime);

            res.Items = await query
                .Skip(paging.Skip)
                .Take(paging.Take)
                .ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Order?> GetOrderByIdAsync(int orderId, CancellationToken cancelToken)
        {
            return await _dbContext.Orders
                .Include(o => o.OrderItems.OrderBy(i => i.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
        }

        /// <summary>Returns next order number for the site (e.g. 1001, 1002). Caller can assign to new order.</summary>
        public async Task<string> GetNextOrderNumberForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var maxNum = await _dbContext.Orders
                .Where(o => o.SiteId == siteId && !o.IsDeleted)
                .Select(o => o.OrderNumber)
                .ToListAsync(cancelToken);

            int next = 1;
            foreach (var s in maxNum)
            {
                if (int.TryParse(s, out var n) && n >= next)
                    next = n + 1;
            }
            return next.ToString();
        }

        public async Task<Order> CreateOrderAsync(Order order, List<OrderItem> items, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(order.OrderNumber))
                order.OrderNumber = await GetNextOrderNumberForSiteAsync(order.SiteId, cancelToken).ConfigureAwait(false);

            _dbContext.Orders.Add(order);
            foreach (var item in items)
            {
                item.OrderId = 0;
                order.OrderItems.Add(item);
            }
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return order;
        }

        public async Task<Order?> UpdateOrderAsync(int orderId, Action<Order> apply, CancellationToken cancelToken)
        {
            var db = await _dbContext.Orders
                .Include(o => o.OrderItems)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            apply(db);
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        /// <summary>Set status to Cancelled and optionally set IsDeleted.</summary>
        public async Task<Order?> CancelOrderAsync(int orderId, int? updateUserId, bool softDelete, CancellationToken cancelToken)
        {
            var db = await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            db.Status = "Cancelled";
            db.UpdatedDate = DateTime.UtcNow;
            db.UpdateUserId = updateUserId;
            if (softDelete) db.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }
    }
}
