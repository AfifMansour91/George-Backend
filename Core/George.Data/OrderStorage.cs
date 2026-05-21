using System.Linq;
using George.Common;
using George.Common.Payment;
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
                .Include(o => o.Site)
                .Include(o => o.Account)
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .AsNoTracking();

            if (filter?.SiteId.HasValue == true)
                query = query.Where(o => o.SiteId == filter.SiteId!.Value);

            if (filter?.Status != null && filter.Status.Count > 0)
            {
                var statuses = filter.Status.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()).ToList();
                if (statuses.Count == 1)
                    query = query.Where(o => o.Status == statuses[0]);
                else if (statuses.Count > 1)
                    query = query.Where(o => statuses.Contains(o.Status));
            }
            else
                query = query.Where(o => o.Status != "Completed" && o.Status != "Cancelled");

            if (filter?.Source.HasValue() == true)
                query = query.Where(o => o.Source == filter.Source!.Trim());

            if (filter?.DeliveryType.HasValue() == true)
                query = query.Where(o => o.DeliveryType == filter.DeliveryType!.Trim());

            if (filter?.PaymentStatus.HasValue() == true)
                query = query.Where(o => o.PaymentStatus == filter.PaymentStatus!.Trim());

            if (filter?.DeliveryDateFrom.HasValue == true)
            {
                var fromDate = filter.DeliveryDateFrom!.Value.Date;
                query = query.Where(o =>
                    (o.DeliveryDate ?? o.PickupDate) != null &&
                    ((o.DeliveryDate ?? o.PickupDate)!.Value.Date >= fromDate));
            }

            if (filter?.DeliveryDateTo.HasValue == true)
            {
                var toDate = filter.DeliveryDateTo!.Value.Date;
                query = query.Where(o =>
                    (o.DeliveryDate ?? o.PickupDate) != null &&
                    ((o.DeliveryDate ?? o.PickupDate)!.Value.Date <= toDate));
            }

            if (filter?.Search?.SearchTerm.HasValue() == true)
            {
                var term = filter.Search.SearchTerm!.Trim();
                query = query.Where(o =>
                    (o.OrderNumber != null && o.OrderNumber.Contains(term)) ||
                    (o.ExternalOrderId != null && o.ExternalOrderId.Contains(term)) ||
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
                .Include(o => o.Site)
                .Include(o => o.Account)
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
        }

        /// <summary>Get order by site and external (e.g. WooCommerce) order id.</summary>
        public async Task<Order?> GetOrderBySiteAndExternalIdAsync(int siteId, string externalOrderId, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(externalOrderId)) return null;
            return await _dbContext.Order
                .Include(o => o.Site)
                .Include(o => o.Account)
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .FirstOrDefaultAsync(o => !o.IsDeleted && o.SiteId == siteId && o.ExternalOrderId == externalOrderId, cancelToken);
        }

        public async Task<Order?> GetOrderByLowProfileIdAsync(string lowProfileId, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(lowProfileId)) return null;
            return await _dbContext.Order
                .Include(o => o.Site)
                .Include(o => o.CustomerPaymentMethod)
                .FirstOrDefaultAsync(o => !o.IsDeleted && o.CardcomLowProfileId == lowProfileId, cancelToken);
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
            //FillOrderHeaderTotalsFromLinesIfMissing(order);
            SnapshotOriginalOrderTotalsIfUnset(order);
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return order;
        }

        public async Task<Order?> UpdateOrderAsync(int orderId, Action<Order> apply, CancellationToken cancelToken)
        {
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .Include(o => o.Site)
                .Include(o => o.Account)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            apply(db);
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        public async Task SetOrderCompletionInventoryAppliedAsync(int orderId, bool value, CancellationToken cancelToken)
        {
            var o = await _dbContext.Order.FirstOrDefaultAsync(x => x.Id == orderId && !x.IsDeleted, cancelToken).ConfigureAwait(false);
            if (o == null) return;
            o.CompletionInventoryApplied = value;
            o.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// After ordered quantities were deducted from catalog (Woo ingest or internal create), set <see cref="OrderItem.PickedQuantity"/>
        /// to the same stock units that were consumed (kg for fixed-weight unit lines, piece count otherwise) so picking deltas only adjust beyond the order,
        /// and vouchers do not show a bogus picked weight (e.g. 2 ק"ג meaning 2 יח').
        /// Sets <see cref="Order.CompletionInventoryApplied"/> so completion-time stock does not run again for the same consumption.
        /// </summary>
        public async Task SetOrderedCatalogConsumedAndBaselinePickingAsync(int orderId, CancellationToken cancelToken)
        {
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken)
                .ConfigureAwait(false);
            if (db == null) return;
            db.CompletionInventoryApplied = true;
            foreach (var item in db.OrderItem.Where(i => !i.IsDeleted))
            {
                if (item.ProductId is > 0 && item.Quantity > 0m)
                {
                    item.PickedQuantity = OrderItemStockConsumption.ResolveOrderedCatalogConsumption(item);
                    item.PickingUserConfirmed = false;
                }
            }
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets <see cref="OrderItem.PickedQuantity"/> to ordered catalog consumption (see <see cref="OrderItemStockConsumption.ResolveOrderedCatalogConsumption"/>)
        /// only for the given line ids (e.g. lines just added via AddItems). Does not change <see cref="Order.CompletionInventoryApplied"/> or other order lines.
        /// </summary>
        public async Task SetPickedQuantityBaselineForOrderItemIdsAsync(
            int orderId,
            IReadOnlyCollection<int> orderItemIds,
            CancellationToken cancelToken)
        {
            if (orderItemIds == null || orderItemIds.Count == 0) return;
            var idSet = orderItemIds.Where(id => id > 0).ToHashSet();
            if (idSet.Count == 0) return;
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken)
                .ConfigureAwait(false);
            if (db == null) return;
            var touched = false;
            foreach (var item in db.OrderItem.Where(i => !i.IsDeleted && idSet.Contains(i.Id)))
            {
                if (item.ProductId is > 0 && item.Quantity > 0m)
                {
                    item.PickedQuantity = OrderItemStockConsumption.ResolveOrderedCatalogConsumption(item);
                    item.PickingUserConfirmed = false;
                    touched = true;
                }
            }
            if (!touched) return;
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
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

        /// <summary>Replace all line items of an order (e.g. WooCommerce sync update). Removes existing items and adds the new list.</summary>
        public async Task<Order?> ReplaceOrderItemsAsync(int orderId, List<OrderItem> newItems, CancellationToken cancelToken)
        {
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            db.OrderItem.Clear();
            for (var i = 0; i < newItems.Count; i++)
            {
                var item = newItems[i];
                item.OrderId = orderId;
                item.SortOrder = i;
                db.OrderItem.Add(item);
            }
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        /// <summary>Remove a single order item (e.g. from picking "הסר מוצר"). Soft delete. Returns updated order or null.</summary>
        public async Task<Order?> RemoveOrderItemAsync(int orderId, int orderItemId, CancellationToken cancelToken)
        {
            var item = await _dbContext.OrderItem
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(i => i.Id == orderItemId && i.OrderId == orderId, cancelToken);
            if (item == null) return null;
            item.IsDeleted = true;
            item.UpdatedDate = DateTime.UtcNow;
            var order = await _dbContext.Order.FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (order != null)
                order.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return await _dbContext.Order
                .Include(o => o.Site)
                .Include(o => o.Account)
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
        }

        /// <summary>Save picked quantity (and optional line total) for order items (שמור וצא).</summary>
        public async Task<Order?> UpdatePickingAsync(
            int orderId,
            List<(int OrderItemId, decimal? PickedQuantity, decimal? TotalPrice, bool? PickingUserConfirmed)> updates,
            CancellationToken cancelToken)
        {
            if (updates == null || updates.Count == 0) return null;
            var db = await _dbContext.Order
                .Include(o => o.OrderItem)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted, cancelToken);
            if (db == null) return null;
            var itemMap = db.OrderItem?.ToDictionary(i => i.Id) ?? new Dictionary<int, OrderItem>();
            foreach (var (orderItemId, pickedQty, totalPrice, confirmFromClient) in updates)
            {
                if (!itemMap.TryGetValue(orderItemId, out var item)) continue;
                var prevPicked = item.PickedQuantity;
                var prevTotal = item.TotalPrice;

                item.PickedQuantity = pickedQty;
                item.TotalPrice = totalPrice;

                if (confirmFromClient == true)
                    item.PickingUserConfirmed = true;
                else if (confirmFromClient == false)
                    item.PickingUserConfirmed = false;
                else if (!NullableDecimalEquals(pickedQty, prevPicked) || !NullableDecimalEquals(totalPrice, prevTotal))
                    item.PickingUserConfirmed = true;
                // else: unchanged vs DB — leave PickingUserConfirmed as-is (avoids marking every line picked when client sends full cart)
            }
            RecalculateOrderHeaderTotalsFromLines(db);
            db.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
            return db;
        }

        private static bool NullableDecimalEquals(decimal? a, decimal? b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            return a.Value == b.Value;
        }

        /// <summary>Merchandise counted toward order subtotal after picking: only lines with picked qty &gt; 0 (0 is "not picked", not null-only).</summary>
        private static decimal SumOrderLineMerchandise(OrderItem i)
        {
            if (!i.PickedQuantity.HasValue || i.PickedQuantity.Value <= 0m)
                return 0m;
            if (i.TotalPrice.HasValue)
                return i.TotalPrice.Value;
            return i.PickedQuantity.Value * (i.PricePerUnit ?? 0m);
        }

        //private static void FillOrderHeaderTotalsFromLinesIfMissing(Order order)
        //{
        //    var active = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
        //    if (active.Count == 0)
        //        return;
        //    var sum = active.Sum(SumOrderLineMerchandise);
        //    if (order.SubTotal == null)
        //        order.SubTotal = sum;
        //    if (order.Total == null && order.SubTotal != null)
        //        order.Total = order.SubTotal.Value + (order.ShippingCost ?? 0m);
        //}

        /// <summary>One-time snapshot of subtotal/total at creation. Does not run when Original* already set (e.g. future manual import).</summary>
        private static void SnapshotOriginalOrderTotalsIfUnset(Order order)
        {
            if (order.OriginalSubTotal != null || order.OriginalTotal != null)
                return;
            order.OriginalSubTotal = order.SubTotal;
            var ship = order.ShippingCost ?? 0m;
            order.OriginalTotal = order.Total ?? (order.SubTotal.HasValue ? order.SubTotal.Value + ship : null);
        }

        /// <summary>After picking: refresh header SubTotal/Total from lines + shipping (Original* unchanged).</summary>
        private static void RecalculateOrderHeaderTotalsFromLines(Order order)
        {
            var active = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            var sum = active.Sum(SumOrderLineMerchandise);
            order.SubTotal = sum;
            order.Total = sum + (order.ShippingCost ?? 0m);
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

        private static bool IsCardcomCreditPaymentMethod(string? method)
        {
            if (string.IsNullOrWhiteSpace(method)) return false;
            var m = method.Trim();
            return m.Equals("SavedCard", StringComparison.OrdinalIgnoreCase)
                || m.Equals("CreditCard", StringComparison.OrdinalIgnoreCase)
                || m.Equals("CreditPhone", StringComparison.OrdinalIgnoreCase)
                || m.Equals("CreditSms", StringComparison.OrdinalIgnoreCase);
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

            foreach (var o in orders)
            {
                if (!IsCardcomCreditPaymentMethod(o.PaymentMethod))
                    continue;

                var hasTokenOnOrder = o.CustomerPaymentMethodId != null
                    || !string.IsNullOrWhiteSpace(o.CardcomTokenLast4)
                    || string.Equals(o.PaymentSettleStatus, PaymentSettleStatus.Authorized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(o.PaymentSettleStatus, PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase);

                if (!hasTokenOnOrder)
                    continue;

                var last4 = o.CardcomTokenLast4;
                var brand = o.CardcomCardBrand;
                if (o.CustomerPaymentMethodId is int pmId)
                {
                    var pm = await _dbContext.CustomerPaymentMethod.AsNoTracking()
                        .FirstOrDefaultAsync(m => m.Id == pmId && !m.IsRetired, cancelToken)
                        .ConfigureAwait(false);
                    if (pm != null)
                    {
                        last4 = pm.Last4Digits ?? last4;
                        brand = pm.CardBrand ?? brand;
                    }
                }

                result.HasSavedCard = true;
                result.SavedCardLast4 = last4;
                result.SavedCardBrand = brand;
                break;
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
                .Include(o => o.Site)
                .Include(o => o.Account)
                .Include(o => o.OrderItem.OrderBy(i => i.SortOrder))
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == lastOrderId.Value && !o.IsDeleted, cancelToken).ConfigureAwait(false);
        }

        /// <summary>Get distinct product IDs that the customer (by phone at site) has ordered in the past. Used for kiosk "past purchases" list.</summary>
        public async Task<List<int>> GetDistinctProductIdsOrderedByCustomerPhoneAsync(int siteId, string? phone, CancellationToken cancelToken)
        {
            if (siteId <= 0 || string.IsNullOrWhiteSpace(phone)) return new List<int>();
            var normalized = NormalizePhone(phone);
            if (normalized.Length < 4) return new List<int>();

            var siteOrders = await _dbContext.Order
                .AsNoTracking()
                .Where(o => !o.IsDeleted && o.SiteId == siteId && o.CustomerPhone != null)
                .OrderByDescending(o => o.CreationTime)
                .Take(500)
                .Select(o => new { o.Id, o.CustomerPhone })
                .ToListAsync(cancelToken).ConfigureAwait(false);

            var orderIds = siteOrders
                .Where(o => NormalizePhone(o.CustomerPhone) == normalized)
                .Select(o => o.Id)
                .ToList();
            if (orderIds.Count == 0) return new List<int>();

            var productIds = await _dbContext.Order
                .AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .SelectMany(o => o.OrderItem)
                .Where(i => i.ProductId != null && i.ProductId.Value > 0)
                .Select(i => i.ProductId!.Value)
                .Distinct()
                .ToListAsync(cancelToken).ConfigureAwait(false);

            return productIds;
        }

        /// <summary>Append a status transition when status changes (Kanban, API, WooCommerce).</summary>
        public async Task AppendOrderStatusHistoryAsync(
            int orderId,
            string status,
            DateTime occurredAtUtc,
            CancellationToken cancelToken)
        {
            if (orderId <= 0 || string.IsNullOrWhiteSpace(status)) return;
            var normalized = status.Trim();
            var last = await _dbContext.OrderStatusHistory
                .Where(h => h.OrderId == orderId)
                .OrderByDescending(h => h.OccurredAt)
                .ThenByDescending(h => h.Id)
                .Select(h => h.Status)
                .FirstOrDefaultAsync(cancelToken)
                .ConfigureAwait(false);
            if (last != null && string.Equals(last, normalized, StringComparison.OrdinalIgnoreCase))
                return;

            _dbContext.OrderStatusHistory.Add(new OrderStatusHistory
            {
                OrderId = orderId,
                Status = normalized,
                OccurredAt = occurredAtUtc.Kind == DateTimeKind.Utc
                    ? occurredAtUtc
                    : occurredAtUtc.ToUniversalTime(),
            });
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }

        public async Task<List<OrderStatusHistory>> GetOrderStatusHistoryAsync(int orderId, CancellationToken cancelToken)
        {
            return await _dbContext.OrderStatusHistory
                .AsNoTracking()
                .Where(h => h.OrderId == orderId)
                .OrderBy(h => h.OccurredAt)
                .ThenBy(h => h.Id)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
        }

        public async Task<Dictionary<int, List<(string Status, DateTime OccurredAt)>>> GetStatusHistoryByOrderIdsAsync(
            IReadOnlyCollection<int> orderIds,
            CancellationToken cancelToken)
        {
            if (orderIds == null || orderIds.Count == 0)
                return new Dictionary<int, List<(string, DateTime)>>();

            var rows = await _dbContext.OrderStatusHistory
                .AsNoTracking()
                .Where(h => orderIds.Contains(h.OrderId))
                .OrderBy(h => h.OrderId)
                .ThenBy(h => h.OccurredAt)
                .ThenBy(h => h.Id)
                .Select(h => new { h.OrderId, h.Status, h.OccurredAt })
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            return rows
                .GroupBy(r => r.OrderId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(r => (r.Status, r.OccurredAt)).ToList());
        }

        /// <summary>Completed orders for site whose last update falls in [fromUtc, toUtcExclusive).</summary>
        public async Task<List<Order>> GetCompletedOrdersInRangeAsync(
            int siteId,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            CancellationToken cancelToken)
        {
            return await _dbContext.Order
                .AsNoTracking()
                .Where(o =>
                    !o.IsDeleted &&
                    o.SiteId == siteId &&
                    o.Status == "Completed" &&
                    (o.UpdatedDate ?? o.CreationTime) >= fromUtc &&
                    (o.UpdatedDate ?? o.CreationTime) < toUtcExclusive)
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);
        }

        /// <summary>
        /// Orders used for handling KPIs: became Ready in the period (בטיפול→מוכן), not only Completed.
        /// Falls back to completed-in-range when no Ready transitions exist (legacy / backfill-only DB).
        /// </summary>
        public async Task<List<Order>> GetOrdersForHandlingMetricsInRangeAsync(
            int siteId,
            DateTime fromUtc,
            DateTime toUtcExclusive,
            CancellationToken cancelToken)
        {
            var readyInRangeIds = await (
                from h in _dbContext.OrderStatusHistory.AsNoTracking()
                join o in _dbContext.Order.AsNoTracking() on h.OrderId equals o.Id
                where !o.IsDeleted
                      && o.SiteId == siteId
                      && h.Status == "Ready"
                      && h.OccurredAt >= fromUtc
                      && h.OccurredAt < toUtcExclusive
                select h.OrderId
            )
                .Distinct()
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            if (readyInRangeIds.Count > 0)
            {
                return await _dbContext.Order
                    .AsNoTracking()
                    .Where(o => readyInRangeIds.Contains(o.Id))
                    .ToListAsync(cancelToken)
                    .ConfigureAwait(false);
            }

            // Orders touched in period that already have InTreatment + Ready in history (timezone / backfill edge cases).
            var fallbackIds = await (
                from o in _dbContext.Order.AsNoTracking()
                where !o.IsDeleted
                      && o.SiteId == siteId
                      && (o.UpdatedDate ?? o.CreationTime) >= fromUtc
                      && (o.UpdatedDate ?? o.CreationTime) < toUtcExclusive
                      && _dbContext.OrderStatusHistory.Any(h =>
                          h.OrderId == o.Id && h.Status == "InTreatment")
                      && _dbContext.OrderStatusHistory.Any(h =>
                          h.OrderId == o.Id && h.Status == "Ready")
                select o.Id
            )
                .Distinct()
                .ToListAsync(cancelToken)
                .ConfigureAwait(false);

            if (fallbackIds.Count > 0)
            {
                return await _dbContext.Order
                    .AsNoTracking()
                    .Where(o => fallbackIds.Contains(o.Id))
                    .ToListAsync(cancelToken)
                    .ConfigureAwait(false);
            }

            return await GetCompletedOrdersInRangeAsync(siteId, fromUtc, toUtcExclusive, cancelToken)
                .ConfigureAwait(false);
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
        public bool HasSavedCard { get; set; }
        public string? SavedCardLast4 { get; set; }
        public string? SavedCardBrand { get; set; }
    }
}
