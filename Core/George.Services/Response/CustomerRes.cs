namespace George.Services.Response;

/// <summary>CRM: Customer list item (matches frontend CustomerRes).</summary>
public class CustomerRes
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? Notes { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public bool MarketingApproval { get; set; }
    public string? CreatedAt { get; set; }
    public bool? IsReturning { get; set; }
    public int? LastOrderId { get; set; }
    /// <summary>ISO 8601 (UTC) date of the customer's most recent order at this site, or null when none. Used for active / at-risk-of-churn filtering on the client.</summary>
    public string? LastOrderDate { get; set; }

    /// <summary>Personal inactivity threshold (days): single-order customers use the site default; customers with history use avg gap × 2.</summary>
    public int? ChurnThresholdDays { get; set; }
    /// <summary>Has ≥1 order and last order is within the personal threshold.</summary>
    public bool IsActive { get; set; }
    /// <summary>Has ≥1 order and last order passed the personal threshold (at risk of churn).</summary>
    public bool IsAtRisk { get; set; }
}
