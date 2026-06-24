namespace George.Services.Response;

/// <summary>CRM: Customer KPIs (matches frontend CustomerStatsRes).
/// Trend fields compare the last 30 days vs the prior 30 days; null when not enough history (HasComparison=false).</summary>
public class CustomerStatsRes
{
    /// <summary>All customers at the site (including those with 0 orders).</summary>
    public int TotalCustomers { get; set; }
    public int? TotalCustomersTrendPercent { get; set; }

    /// <summary>Customers whose last order is within their personal inactivity threshold.</summary>
    public int ActiveCustomers { get; set; }
    public decimal? ActiveCustomersTrendPercent { get; set; }

    /// <summary>Average order value over the last 30 days (sum of order totals ÷ order count).</summary>
    public decimal Aov { get; set; }
    public decimal? AovTrendPercent { get; set; }

    /// <summary>Total orders ÷ customers with at least one order.</summary>
    public decimal AverageOrdersPerCustomer { get; set; }
    /// <summary>Absolute change (in orders) vs 30 days ago.</summary>
    public decimal? AverageOrdersPerCustomerTrend { get; set; }

    /// <summary>Average gap (days) between orders, over returning customers (2+ orders).</summary>
    public int AverageReturnDays { get; set; }
    /// <summary>Absolute change (in days) vs 30 days ago.</summary>
    public decimal? AverageReturnDaysTrend { get; set; }

    /// <summary>Customers with orders who passed their personal inactivity threshold (at risk of churn).</summary>
    public int AtRiskCustomers { get; set; }
    public decimal? AtRiskCustomersTrendPercent { get; set; }

    /// <summary>Default inactivity threshold (days) configured for the site (single-order customers).</summary>
    public int ChurnThresholdDays { get; set; }

    /// <summary>True when the site has at least 30 days of order history, so 30-day trends are meaningful.</summary>
    public bool HasComparison { get; set; }

    // --- Legacy (kept for backward compatibility; no longer shown on the customers screen) ---
    public int ReturningCustomersPercent { get; set; }
}
