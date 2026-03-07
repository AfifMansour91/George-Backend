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
}
