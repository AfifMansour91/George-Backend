namespace George.Services.Response;

/// <summary>Customer profile for manual order: lookup by phone at a site. Derived from order history.</summary>
public class CustomerProfileRes
{
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    /// <summary>Manager note from the most recent order (or persistent note when supported).</summary>
    public string? ManagerNote { get; set; }
    /// <summary>Last order date (ISO date string).</summary>
    public string? LastOrderDate { get; set; }
    public int OrderCount { get; set; }
    public decimal? AverageOrderTotal { get; set; }
    public decimal? TotalTransactions { get; set; }
    public bool Found { get; set; }
}
