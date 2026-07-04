using George.Common;
using George.DB;
using George.Services.Orders;
using George.Services.Request;
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

    /// <summary>A promotion resolved from a WooCommerce order payload, ready to record as a redemption.</summary>
    internal readonly struct ResolvedOrderPromotion
    {
        public int PromotionId { get; init; }
        public decimal DiscountAmount { get; init; }
        public decimal RevenueNis { get; init; }
    }

    /// <summary>
    /// Stamp order lines from the promotions WooCommerce actually applied (Spec §1.2), instead of
    /// re-running George's evaluator on a web order. Lines are matched by WooCommerce product id / sku
    /// for per-line display; the returned tuples carry the authoritative per-promotion discount for
    /// idempotent metric recording. Only promotions that map to an existing George promotion
    /// (<c>external_id = george-{id}</c>) for this site are stamped — locally-authored WooCommerce
    /// promotions (no external id) are skipped.
    /// </summary>
    private async Task<IReadOnlyList<ResolvedOrderPromotion>> StampPromotionsFromPayloadAsync(
        int siteId,
        List<OrderItem> items,
        WooCommerceOrderPayload payload,
        CancellationToken cancelToken)
    {
        var resolved = new List<ResolvedOrderPromotion>();
        var applied = payload.AppliedPromotions;
        if (applied == null || applied.Count == 0 || items.Count == 0) return resolved;

        foreach (var promo in applied)
        {
            if (!PromotionPromengWireMapper.TryParseExternalId(promo.ExternalId, out var promotionId))
                continue; // local WooCommerce promotion — not a George promotion, can't link.

            var georgePromo = await _promotionStorage.GetPromotionAsync(promotionId, cancelToken).ConfigureAwait(false);
            if (georgePromo == null || georgePromo.IsDeleted || georgePromo.SiteId != siteId)
            {
                _logger.LogWarning(
                    "Applied promotion {ExternalId} (george id {PromotionId}) not found for site {SiteId}; skipping stamp.",
                    promo.ExternalId, promotionId, siteId);
                continue;
            }

            decimal revenue = 0m;
            var lines = promo.Lines;
            if (lines != null && lines.Count > 0)
            {
                foreach (var line in lines)
                {
                    var target = MatchOrderItemForPromotionLine(items, line);
                    if (target == null) continue;
                    target.PromotionId = promotionId;
                    target.DiscountAmount = (target.DiscountAmount ?? 0m) + Math.Max(0m, line.DiscountAmount);
                    revenue += Math.Max(0m, (target.TotalPrice ?? 0m) - Math.Max(0m, line.DiscountAmount));
                }
            }
            else if (promo.DiscountAmount > 0m)
            {
                // Fallback: the plugin sent the promotion at order level only (spec §1.3 per-line `lines[]`
                // not yet emitted). Distribute the order-level discount across the order lines proportionally
                // by line total, so the promotion is still stamped, recorded, and visible in picking. Each
                // line is capped at its own total; the last eligible line absorbs the rounding remainder.
                revenue += DistributeOrderLevelPromotionDiscount(items, promotionId, promo.DiscountAmount);
            }

            resolved.Add(new ResolvedOrderPromotion
            {
                PromotionId = promotionId,
                DiscountAmount = Math.Max(0m, promo.DiscountAmount),
                RevenueNis = revenue,
            });
        }

        return resolved;
    }

    /// <summary>
    /// Spread an order-level promotion discount (no per-line <c>lines[]</c> from the plugin) across the order
    /// lines proportionally by line total. Only lines with a positive total are eligible; each line's share is
    /// capped at its own total and at the remaining discount, and the last eligible line takes the remainder so
    /// the stamped discounts sum exactly to <paramref name="discountAmount"/> (bounded by the merchandise total).
    /// Returns the post-discount revenue of the stamped lines (for redemption metrics).
    /// </summary>
    private static decimal DistributeOrderLevelPromotionDiscount(
        List<OrderItem> items,
        int promotionId,
        decimal discountAmount)
    {
        var eligible = items.Where(i => !i.IsDeleted && (i.TotalPrice ?? 0m) > 0m).ToList();
        var baseTotal = eligible.Sum(i => i.TotalPrice ?? 0m);
        if (eligible.Count == 0 || baseTotal <= 0m) return 0m;

        var remaining = Math.Min(Math.Max(0m, discountAmount), baseTotal);
        decimal revenue = 0m;
        for (var k = 0; k < eligible.Count; k++)
        {
            var it = eligible[k];
            var lineTotal = it.TotalPrice ?? 0m;
            var share = k == eligible.Count - 1
                ? remaining // last eligible line absorbs the rounding remainder
                : Math.Round(discountAmount * (lineTotal / baseTotal), 2);
            share = Math.Min(Math.Min(share, lineTotal), remaining);
            if (share <= 0m) continue;
            it.PromotionId = promotionId;
            it.DiscountAmount = (it.DiscountAmount ?? 0m) + share;
            revenue += Math.Max(0m, lineTotal - share);
            remaining -= share;
        }
        return revenue;
    }

    /// <summary>Match a payload promotion line to an order line by WooCommerce product id, then sku, preferring an un-stamped line.</summary>
    private static OrderItem? MatchOrderItemForPromotionLine(
        List<OrderItem> items,
        Request.WooCommerceAppliedPromotionLinePayload line)
    {
        bool ByWooId(OrderItem i) => line.ProductId is > 0 && i.WooCommerceProductId == line.ProductId;
        bool BySku(OrderItem i) => !string.IsNullOrWhiteSpace(line.Sku)
            && string.Equals(i.LineSku, line.Sku, StringComparison.OrdinalIgnoreCase);

        return items.FirstOrDefault(i => !i.IsDeleted && i.PromotionId == null && ByWooId(i))
            ?? items.FirstOrDefault(i => !i.IsDeleted && ByWooId(i))
            ?? items.FirstOrDefault(i => !i.IsDeleted && i.PromotionId == null && BySku(i))
            ?? items.FirstOrDefault(i => !i.IsDeleted && BySku(i));
    }

    /// <summary>
    /// Resolve the promotions on a WooCommerce order: trust the payload's <c>appliedPromotions</c> when present
    /// (stamps lines from them, no re-evaluation), else fall back to George's evaluator (transition / older plugin).
    /// </summary>
    private async Task<IReadOnlyList<ResolvedOrderPromotion>> ResolveWooCommerceOrderPromotionsAsync(
        int siteId,
        string? source,
        int customerId,
        string? couponCode,
        string? customerPhone,
        List<OrderItem> items,
        WooCommerceOrderPayload payload,
        IReadOnlyDictionary<int, Product?>? productCache,
        CancellationToken cancelToken)
    {
        if (payload.AppliedPromotions != null)
            return await StampPromotionsFromPayloadAsync(siteId, items, payload, cancelToken).ConfigureAwait(false);

        await ApplyPromotionsToOrderItemsAsync(
            siteId, source, customerId, couponCode, customerPhone, items, productCache, cancelToken)
            .ConfigureAwait(false);
        return ResolvedPromotionsFromStampedItems(items);
    }

    /// <summary>Persist an order's promotion redemptions idempotently (KPI delta applied once per order+promotion). Best-effort: never blocks the order.</summary>
    private async Task PersistOrderPromotionRedemptionsAsync(
        int siteId,
        int orderId,
        string? externalOrderId,
        string? source,
        DateTime redeemedAtUtc,
        IReadOnlyList<ResolvedOrderPromotion> promos,
        CancellationToken cancelToken)
    {
        if (promos.Count == 0) return;
        var rows = promos.Select(p => (p.PromotionId, p.DiscountAmount, p.RevenueNis)).ToList();
        try
        {
            await _promotionStorage
                .RecordOrderPromotionRedemptionsAsync(
                    siteId, orderId, externalOrderId, MapOrderSourceToPromotionChannel(source),
                    redeemedAtUtc, rows, cancelToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RecordOrderPromotionRedemptionsAsync failed orderId={OrderId}", orderId);
        }
    }

    /// <summary>Build redemption tuples from already-stamped order lines (used by the fallback re-evaluation path).</summary>
    private static IReadOnlyList<ResolvedOrderPromotion> ResolvedPromotionsFromStampedItems(IEnumerable<OrderItem> items) =>
        items
            .Where(i => !i.IsDeleted && i.PromotionId is > 0)
            .GroupBy(i => i.PromotionId!.Value)
            .Select(g => new ResolvedOrderPromotion
            {
                PromotionId = g.Key,
                DiscountAmount = g.Sum(x => x.DiscountAmount ?? 0m),
                RevenueNis = g.Sum(x => (x.TotalPrice ?? 0m) - (x.DiscountAmount ?? 0m)),
            })
            .ToList();

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
        if (order == null) return;

        var items = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
        if (items.Count == 0) return;

        // Internal orders (phone/kiosk/manual) always re-evaluate promotions during picking. A WEBSITE order
        // does too — but only once it carries a George-linked promotion (stamped from the Woo
        // appliedPromotions on ingest). This makes such an order behave like an internal one: products added
        // during picking join the promotion and the final order is recomputed (spec §2 — George becomes the
        // source of truth at picking). A website order with no promotion is left untouched (trust Woo).
        var hasGeorgePromotion = items.Any(i => i.PromotionId is > 0);
        if (!ShouldReapplyPromotionsDuringPicking(order.Source) && !hasGeorgePromotion) return;

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

        // Picking can change the discount, or add/drop a promotion entirely (threshold met/not met
        // after weighing). Re-sync the redemption rows + KPIs from the re-stamped lines so metrics
        // reflect the picked reality instead of the order-time stamp.
        try
        {
            await _promotionStorage
                .ReverseOrderPromotionRedemptionsAsync(order.SiteId, order.Id, cancelToken)
                .ConfigureAwait(false);
            await PersistOrderPromotionRedemptionsAsync(
                order.SiteId,
                order.Id,
                order.ExternalOrderId,
                order.Source,
                order.CreationTime,
                ResolvedPromotionsFromStampedItems(items),
                cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Re-syncing promotion redemptions after picking failed orderId={OrderId}", orderId);
        }
    }
}
