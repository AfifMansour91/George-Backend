namespace George.Common;

/// <summary>
/// Shared order discount math for picking recalc, vouchers, and API enrichment.
/// </summary>
public static class OrderDiscountTotals
{
    public static decimal SumLinePromotionDiscount(IEnumerable<(decimal? DiscountAmount, bool IsDeleted)> lines) =>
        lines.Where(l => !l.IsDeleted && l.DiscountAmount is > 0m).Sum(l => l.DiscountAmount!.Value);

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
