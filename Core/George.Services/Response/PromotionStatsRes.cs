namespace George.Services.Response;

public class PromotionStatsRes
{
    public int TabAll { get; set; }
    public int TabActive { get; set; }
    public int TabScheduled { get; set; }
    public int TabDrafts { get; set; }
    public int TabEnded { get; set; }

    public int EndingWithinWeek { get; set; }

    public decimal PeriodRevenueNis { get; set; }
    public decimal PeriodDiscountNis { get; set; }
    public int PeriodRedemptions { get; set; }

    /// <summary>Revenue ÷ discount amount for the period (0 if no discount).</summary>
    public decimal YieldPerDiscountNis { get; set; }

    /// <summary>Promotion-attributed revenue as percent of site order total in the same period (null if no orders).</summary>
    public int? RevenuePctOfSiteOrders { get; set; }
}
