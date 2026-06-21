using George.Common;
using George.DB;
using George.Services.Response;

namespace George.Services;

public partial class OrderService
{
    private static DateTime SpecifyUtcOut(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private async Task RecordOrderStatusChangeAsync(
        int orderId,
        string? previousStatus,
        string newStatus,
        DateTime occurredAtUtc,
        CancellationToken cancelToken)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(newStatus)) return;
        if (previousStatus != null &&
            string.Equals(previousStatus.Trim(), newStatus.Trim(), StringComparison.OrdinalIgnoreCase))
            return;
        await _orderStorage
            .AppendOrderStatusHistoryAsync(orderId, newStatus.Trim(), occurredAtUtc, cancelToken)
            .ConfigureAwait(false);
    }

    private async Task EnrichOrderResAsync(OrderRes res, Order order, CancellationToken cancelToken)
    {
        var map = await _orderStorage
            .GetStatusHistoryByOrderIdsAsync(new[] { order.Id }, cancelToken)
            .ConfigureAwait(false);
        ApplyStatusTimestampsToRes(res, order, map.GetValueOrDefault(order.Id));
        ApplyPickupBranchDisplayName(res, order);
        await ApplyCustomerProfileNotesAsync(new[] { res }, new[] { order }, cancelToken).ConfigureAwait(false);
    }

    private async Task EnrichOrderResListAsync(
        IReadOnlyList<OrderRes> list,
        IReadOnlyList<Order> orders,
        CancellationToken cancelToken)
    {
        if (list.Count == 0) return;
        var ids = orders.Select(o => o.Id).ToList();
        var map = await _orderStorage.GetStatusHistoryByOrderIdsAsync(ids, cancelToken).ConfigureAwait(false);
        var orderById = orders.ToDictionary(o => o.Id);
        foreach (var res in list)
        {
            if (!orderById.TryGetValue(res.Id, out var order)) continue;
            ApplyStatusTimestampsToRes(res, order, map.GetValueOrDefault(res.Id));
            ApplyPickupBranchDisplayName(res, order);
        }
        await ApplyCustomerProfileNotesAsync(list, orders, cancelToken).ConfigureAwait(false);
    }

    /// <summary>Self-pickup: expose branch name from Site when Woo/manual order has no shippingStoreName.</summary>
    private static void ApplyPickupBranchDisplayName(OrderRes res, Order order)
    {
        if (!string.Equals(order.DeliveryType, "Pickup", StringComparison.OrdinalIgnoreCase))
            return;
        if (!string.IsNullOrWhiteSpace(res.ShippingStoreName))
            return;
        var siteName = order.Site?.SiteName?.Trim();
        if (!string.IsNullOrWhiteSpace(siteName))
            res.ShippingStoreName = siteName;
    }

    private async Task ApplyCustomerProfileNotesAsync(
        IReadOnlyList<OrderRes> list,
        IReadOnlyList<Order> orders,
        CancellationToken cancelToken)
    {
        var customerIds = orders
            .Select(o => o.CustomerId ?? 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
        if (customerIds.Count == 0) return;

        var notesByCustomerId = await _customerStorage
            .GetNotesByCustomerIdsAsync(customerIds, cancelToken)
            .ConfigureAwait(false);

        var orderById = orders.ToDictionary(o => o.Id);
        foreach (var res in list)
        {
            if (!orderById.TryGetValue(res.Id, out var order)) continue;
            if (order.CustomerId is not > 0) continue;
            if (notesByCustomerId.TryGetValue(order.CustomerId.Value, out var notes))
                res.CustomerProfileNote = notes;
        }
        await EnrichOrderResListPromotionFieldsAsync(list, orders, cancelToken).ConfigureAwait(false);
    }

    private static void ApplyStatusTimestampsToRes(
        OrderRes res,
        Order order,
        List<(string Status, DateTime OccurredAt)>? history)
    {
        var rows = history ?? new List<(string, DateTime)>();
        var ts = OrderHandlingMetricsCalculator.FromHistoryRows(rows, order.CreationTime, order.UpdatedDate);
        if (!ts.CompletedAt.HasValue &&
            string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            ts = new OrderHandlingMetricsCalculator.StatusTimestamps
            {
                NewAt = ts.NewAt,
                InTreatmentAt = ts.InTreatmentAt,
                ReadyAt = ts.ReadyAt,
                CompletedAt = order.UpdatedDate ?? order.CreationTime,
            };
        }

        res.NewAt = ts.NewAt.HasValue ? SpecifyUtcOut(ts.NewAt.Value) : SpecifyUtcOut(order.CreationTime);
        res.InTreatmentAt = ts.InTreatmentAt.HasValue ? SpecifyUtcOut(ts.InTreatmentAt.Value) : null;
        res.ReadyAt = ts.ReadyAt.HasValue ? SpecifyUtcOut(ts.ReadyAt.Value) : null;
        res.CompletedAt = ts.CompletedAt.HasValue ? SpecifyUtcOut(ts.CompletedAt.Value) : null;
    }

    public async Task<IApiResponse<List<OrderStatusHistoryEntryRes>>> GetOrderStatusHistoryAsync(
        int orderId,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<OrderStatusHistoryEntryRes>> { Data = new List<OrderStatusHistoryEntryRes>() };
        var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        var rows = await _orderStorage.GetOrderStatusHistoryAsync(orderId, cancelToken).ConfigureAwait(false);
        response.Data = rows.ConvertAll(h => new OrderStatusHistoryEntryRes
        {
            Status = h.Status,
            OccurredAt = SpecifyUtcOut(h.OccurredAt),
        });
        return response;
    }

    public async Task<IApiResponse<OrderHandlingMetricsRes>> GetOrderHandlingMetricsAsync(
        int siteId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancelToken = default) =>
        await GetOrderHandlingMetricsForSitesAsync(new[] { siteId }, fromDate, toDate, cancelToken)
            .ConfigureAwait(false);

    public async Task<IApiResponse<OrderHandlingMetricsRes>> GetOrderHandlingMetricsForSitesAsync(
        IReadOnlyList<int> siteIds,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderHandlingMetricsRes>
        {
            Data = new OrderHandlingMetricsRes(),
        };
        if (siteIds == null || siteIds.Count == 0 || siteIds.All(id => id <= 0))
            return CreateResponse(response, StatusCode.InvalidRequest, "siteId is required.");

        var fromUtc = fromDate.Date.Kind == DateTimeKind.Utc
            ? fromDate.Date
            : DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);
        var toUtcExclusive = toDate.Date.AddDays(1);
        if (toDate.Date.Kind != DateTimeKind.Utc)
            toUtcExclusive = DateTime.SpecifyKind(toUtcExclusive, DateTimeKind.Utc);

        var orders = new List<Order>();
        foreach (var siteId in siteIds.Where(id => id > 0))
        {
            orders.AddRange(await _orderStorage
                .GetOrdersForHandlingMetricsInRangeAsync(siteId, fromUtc, toUtcExclusive, cancelToken)
                .ConfigureAwait(false));
        }

        if (orders.Count == 0)
            return response;

        var historyMap = await _orderStorage
            .GetStatusHistoryByOrderIdsAsync(orders.Select(o => o.Id).ToList(), cancelToken)
            .ConfigureAwait(false);

        var timestamps = new List<OrderHandlingMetricsCalculator.StatusTimestamps>();
        foreach (var order in orders)
        {
            historyMap.TryGetValue(order.Id, out var rows);
            var ts = OrderHandlingMetricsCalculator.FromHistoryRows(
                rows ?? new List<(string, DateTime)>(),
                order.CreationTime,
                order.UpdatedDate);
            if (!ts.CompletedAt.HasValue)
            {
                ts = new OrderHandlingMetricsCalculator.StatusTimestamps
                {
                    NewAt = ts.NewAt,
                    InTreatmentAt = ts.InTreatmentAt,
                    ReadyAt = ts.ReadyAt,
                    CompletedAt = order.UpdatedDate ?? order.CreationTime,
                };
            }

            timestamps.Add(ts);
        }

        response.Data = OrderHandlingMetricsCalculator.Compute(timestamps);
        return response;
    }
}
