using System.Linq;
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
            var query = _dbContext.Order
                .Where(o => !o.IsDeleted)
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
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
            //    query = query.Where(o => (o.DeliveryDate ?? o.PickupDate) >= filter.DeliveryDateFrom);

            //if (filter?.DeliveryDateTo.HasValue == true)
            //    query = query.Where(o => (o.DeliveryDate ?? o.PickupDate) <= filter.DeliveryDateTo);

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
            return await _dbContext.Order
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
        }

        /// <summary>Returns next order number for the site (e.g. 1001, 1002). Caller can assign to new order.</summary>
        public async Task<string> GetNextOrderNumberForSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var maxNum = await _dbContext.Order
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

            _dbContext.Order.Add(order);
            foreach (var item in items)
            {
                item.OrderId = 0;
                order.OrderItem.Add(item);
            }
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return order;
        }

        public async Task<Order?> UpdateOrderAsync(int orderId, Action<Order> apply, CancellationToken cancelToken)
        {
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            apply(db);
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        /// <summary>Add line items to an existing order (e.g. from picking "הוסף פריט").</summary>
        public async Task<Order?> AddOrderItemsAsync(int orderId, List<OrderItem> newItems, CancellationToken cancelToken)
        {
            if (newItems == null || newItems.Count == 0) return null;
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            var maxSort = (db.OrderItem?.Count ?? 0) > 0 ? db.OrderItem!.Max(i => i.SortOrder) : -1;
            for (var i = 0; i < newItems.Count; i++)
            {
                var item = newItems[i];
                item.OrderId = orderId;
                item.SortOrder = maxSort + 1 + i;
                db.OrderItem.Add(item);
            }
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        /// <summary>Update picked quantity (and optional line total) for order items (שמור וצא).</summary>
        public async Task<Order?> UpdatePickingAsync(int orderId, List<(int OrderItemId, decimal? PickedQuantity, decimal? TotalPrice)> updates, CancellationToken cancelToken)
        {
            if (updates == null || updates.Count == 0) return null;
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            var itemMap = db.OrderItem?.ToDictionary(i => i.Id) ?? new Dictionary<int, OrderItem>();
            foreach (var (orderItemId, pickedQty, totalPrice) in updates)
            {
                if (!itemMap.TryGetValue(orderItemId, out var item)) continue;
                if (pickedQty.HasValue) item.PickedQuantity = pickedQty.Value;
                if (totalPrice.HasValue) item.TotalPrice = totalPrice.Value;
            }
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        /// <summary>Set status to Cancelled and optionally set IsDeleted.</summary>
        public async Task<Order?> CancelOrderAsync(int orderId, int? updateUserId, bool softDelete, CancellationToken cancelToken)
        {
            var db = await _dbContext.Order
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            db.Status = "Cancelled";
            db.UpdatedDate = DateTime.UtcNow;
            db.UpdateUserId = updateUserId;
            if (softDelete) db.IsDeleted = true;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        /// <summary>Normalize phone for lookup: digits only (strip spaces, dashes).</summary>
        private static string NormalizePhone(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;
            return new string(phone.Where(char.IsDigit).ToArray());
        }

        /// <summary>Get customer profile by phone at site: name, manager note, and stats from order history.</summary>
        public async Task<CustomerOrderProfile> GetCustomerProfileByPhoneAsync(int siteId, string? phone, CancellationToken cancelToken)
        {
            var result = new CustomerOrderProfile();
            if (siteId <= 0 || string.IsNullOrWhiteSpace(phone))
                return result;

            var normalized = NormalizePhone(phone);
            if (normalized.Length < 4)
                return result;

            var siteOrders = await _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.SiteId == siteId && o.CustomerPhone != null)
                .OrderByDescending(o => o.CreationTime)
                .Take(1000)
                .ToListAsync(cancelToken).ConfigureAwait(false);

            var orders = siteOrders.Where(o => NormalizePhone(o.CustomerPhone) == normalized).ToList();
            if (orders.Count == 0)
                return result;

            var last = orders[0];
            result.Found = true;
            result.CustomerName = last.CustomerName;
            result.CustomerPhone = last.CustomerPhone;
            result.ManagerNote = last.ManagerNote;
            result.LastOrderDate = (last.DeliveryDate ?? last.PickupDate)?.ToString("yyyy-MM-dd");
            result.OrderCount = orders.Count;
            var totals = orders.Where(o => o.Total.HasValue).Select(o => o.Total!.Value).ToList();
            if (totals.Count > 0)
            {
                result.TotalTransactions = totals.Sum();
                result.AverageOrderTotal = totals.Sum() / totals.Count;
            }
            return result;
        }

        /// <summary>Get last order with items by customer phone at site (for "last purchase" quick add).</summary>
        public async Task<Order?> GetLastOrderByCustomerPhoneAsync(int siteId, string? phone, CancellationToken cancelToken)
        {
            if (siteId <= 0 || string.IsNullOrWhiteSpace(phone)) return null;
            var normalized = NormalizePhone(phone);
            if (normalized.Length < 4) return null;

            var siteOrders = await _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.SiteId == siteId && o.CustomerPhone != null)
                .OrderByDescending(o => o.CreationTime)
                .Take(500)
                .ToListAsync(cancelToken).ConfigureAwait(false);

            var lastOrderId = siteOrders.FirstOrDefault(o => NormalizePhone(o.CustomerPhone) == normalized)?.Id;
            if (lastOrderId == null) return null;

            return await _dbContext.Order
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == lastOrderId.Value && !o.IsDeleted, cancelToken).ConfigureAwait(false);
        }
    }

    /// <summary>Internal DTO for customer profile from orders.</summary>
    public class CustomerOrderProfile
    {
        public bool Found { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerPhone { get; set; }
        public string? ManagerNote { get; set; }
        public string? LastOrderDate { get; set; }
        public int OrderCount { get; set; }
        public decimal? AverageOrderTotal { get; set; }
        public decimal? TotalTransactions { get; set; }
    }
}
