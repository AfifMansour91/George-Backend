namespace George.Common;

/// <summary>
/// Shared order discount math for picking recalc, vouchers, and API enrichment.
/// </summary>
public static class OrderDiscountTotals
{
    public static decimal SumLinePromotionDiscount(IEnumerable<(decimal? DiscountAmount, bool IsDeleted)> lines) =>
        lines.Where(l => !l.IsDeleted && l.DiscountAmount is > 0m).Sum(l => l.DiscountAmount!.Value);

    /// <summary>
    /// Effective line discount for a given line gross. A George-linked stamp (<paramref name="promotionId"/> &gt; 0)
    /// is returned as-is - the promotion evaluator re-derives it on picking. An unlinked stamp (a WP-local
    /// promotion) has no evaluator, so it is scaled proportionally when picking changed the line gross
    /// (e.g. a 5% discount stamped on 500g follows the line to the actual picked weight).
    /// </summary>
    public static decimal ScaleStampedLineDiscount(
        decimal? discountAmount,
        int? promotionId,
        decimal orderedGross,
        decimal currentGross)
    {
        var baseDiscount = discountAmount is > 0m ? discountAmount.Value : 0m;
        if (baseDiscount <= 0m) return 0m;
        if (promotionId is > 0) return baseDiscount;
        if (orderedGross <= 0.001m || currentGross <= 0m) return baseDiscount;
        if (Math.Abs(currentGross - orderedGross) <= 0.01m) return baseDiscount;
        return Math.Round(baseDiscount * currentGross / orderedGross, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>The manual (order-level) discount for the merchandise as it stands now: a percent discount follows the
    /// picked merchandise (picking changes weights), an amount is capped at merchandise + shipping. Without type/value
    /// (legacy rows) the stored amount is kept.</summary>
    public static decimal ResolveManualDiscount(string? type, decimal? value, decimal? storedAmount, decimal merchandiseGross, decimal linePromotionDiscount, decimal shipping)
    {
        var stored = storedAmount is > 0m ? storedAmount.Value : 0m;
        if (value is not > 0m || merchandiseGross <= 0m) return stored;
        var net = Math.Max(0m, merchandiseGross - linePromotionDiscount);
        if (string.Equals(type, "percent", StringComparison.OrdinalIgnoreCase))
            return Math.Round(Math.Min(net, net * value.Value / 100m), 2, MidpointRounding.AwayFromZero);
        if (string.Equals(type, "amount", StringComparison.OrdinalIgnoreCase))
            return Math.Min(net + shipping, value.Value);
        return stored;
    }

    public static decimal ComputeGrandTotal(
        decimal merchandiseGross,
        decimal shipping,
        decimal linePromotionDiscount,
        decimal manualDiscount)
    {
        var netMerchandise = Math.Max(0m, merchandiseGross - linePromotionDiscount);
        return Math.Max(0m, netMerchandise + shipping - manualDiscount);
    }
}
