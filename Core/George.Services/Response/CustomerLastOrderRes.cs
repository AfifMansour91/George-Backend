namespace George.Services.Response;

/// <summary>CRM: Last order summary for a customer (matches frontend CustomerLastOrderRes).</summary>
public class CustomerLastOrderRes
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string? Source { get; set; }
    public string? FirstItemTitle { get; set; }
    public int ItemCount { get; set; }
}
