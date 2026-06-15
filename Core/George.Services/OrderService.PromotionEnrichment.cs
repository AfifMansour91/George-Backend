using George.Common;
using George.DB;
using George.Services.Orders;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

public partial class OrderService
{
    private async Task EnrichOrderResPromotionFieldsAsync(
        OrderRes res,
        Order order,
        CancellationToken cancelToken)
    {
        var sourceItems = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
        var promoTotal = OrderDiscountTotals.SumLinePromotionDiscount(
            sourceItems.Select(i => (i.DiscountAmount, i.IsDeleted)));
        res.PromotionDiscountTotal = promoTotal > 0m ? promoTotal : null;

        var promoIds = sourceItems
            .Select(i => i.PromotionId)
            .Where(id => id is > 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        if (promoIds.Count == 0) return;

        var nameById = new Dictionary<int, string>();
        foreach (var pid in promoIds)
        {
            var promo = await _promotionStorage.GetPromotionAsync(pid, cancelToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(promo?.Name))
                nameById[pid] = promo!.Name!.Trim();
        }

        foreach (var itemRes in res.Items)
        {
            if (itemRes.PromotionId is > 0 && nameById.TryGetValue(itemRes.PromotionId.Value, out var name))
                itemRes.PromotionName = name;
        }
    }

    private async Task EnrichOrderResListPromotionFieldsAsync(
        IReadOnlyList<OrderRes> list,
        IReadOnlyList<Order> orders,
        CancellationToken cancelToken)
    {
        if (list.Count == 0) return;
        var orderById = orders.ToDictionary(o => o.Id);
        var allPromoIds = new HashSet<int>();
        foreach (var order in orders)
        {
            foreach (var item in order.OrderItem?.Where(i => !i.IsDeleted) ?? Enumerable.Empty<OrderItem>())
            {
                if (item.PromotionId is > 0) allPromoIds.Add(item.PromotionId.Value);
            }
        }

        var nameById = new Dictionary<int, string>();
        foreach (var pid in allPromoIds)
        {
            var promo = await _promotionStorage.GetPromotionAsync(pid, cancelToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(promo?.Name))
                nameById[pid] = promo!.Name!.Trim();
        }

        foreach (var res in list)
        {
            if (!orderById.TryGetValue(res.Id, out var order)) continue;
            var sourceItems = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            var promoTotal = OrderDiscountTotals.SumLinePromotionDiscount(
                sourceItems.Select(i => (i.DiscountAmount, i.IsDeleted)));
            res.PromotionDiscountTotal = promoTotal > 0m ? promoTotal : null;
            foreach (var itemRes in res.Items)
            {
                if (itemRes.PromotionId is > 0 && nameById.TryGetValue(itemRes.PromotionId.Value, out var name))
                    itemRes.PromotionName = name;
            }
        }
    }

    private readonly struct OrderPromotionEvalLine
    {
        public decimal Quantity { get; init; }
        public decimal PricePerUnit { get; init; }
        public decimal LineTotal { get; init; }
    }

    private static bool ShouldReapplyPromotionsDuringPicking(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        var s = source.Trim();
        return s.Equals("Phone", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Kiosk", StringComparison.OrdinalIgnoreCase)
            || s.Equals("Manual", StringComparison.OrdinalIgnoreCase);
    }

    private static OrderPromotionEvalLine DefaultPromotionEvalLine(OrderItem it)
    {
        var qty = it.Quantity;
        var lineTotal = it.TotalPrice ?? qty * (it.PricePerUnit ?? 0m);
        var ppu = qty > 0m ? lineTotal / qty : (it.PricePerUnit ?? 0m);
        return new OrderPromotionEvalLine { Quantity = qty, PricePerUnit = ppu, LineTotal = lineTotal };
    }

  private static OrderPromotionEvalLine? PickingPromotionEvalLine(OrderItem it)
    {
        if (OrderItemLineDisplay.OrderMeaningfulPick(it))
        {
            var pickedQty = it.PickedQuantity ?? 0m;
            if (pickedQty <= 0m) return null;
            var lineTotal = it.TotalPrice ?? pickedQty * (it.PricePerUnit ?? 0m);
            var ppu = pickedQty > 0m ? lineTotal / pickedQty : (it.PricePerUnit ?? 0m);
            return new OrderPromotionEvalLine { Quantity = pickedQty, PricePerUnit = ppu, LineTotal = lineTotal };
        }

        return DefaultPromotionEvalLine(it);
    }

    /// <summary>
    /// Re-run promotion evaluator after picking qty/weight changes (phone/kiosk orders).
    /// Uses picked amounts for confirmed lines; ordered amounts for not-yet-picked lines.
    /// </summary>
    private async Task TryReapplyOrderPromotionsAfterPickingAsync(int orderId, CancellationToken cancelToken)
    {
        var order = await _orderStorage.GetOrderByIdTrackedAsync(orderId, cancelToken).ConfigureAwait(false);
        if (order == null || !ShouldReapplyPromotionsDuringPicking(order.Source)) return;

        var items = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
        if (items.Count == 0) return;

        foreach (var it in items)
        {
            it.PromotionId = null;
            it.DiscountAmount = null;
        }

        var productCache = new Dictionary<int, Product?>();
        try
        {
            await ApplyPromotionsToOrderItemsAsync(
                order.SiteId,
                order.Source,
                order.CustomerId ?? 0,
                order.CouponCode,
                order.CustomerPhone,
                items,
                productCache,
                cancelToken,
                PickingPromotionEvalLine).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TryReapplyOrderPromotionsAfterPickingAsync failed orderId={OrderId}", orderId);
            return;
        }

        await _orderStorage.PersistTrackedOrderTotalsAsync(order, cancelToken).ConfigureAwait(false);
    }
}
