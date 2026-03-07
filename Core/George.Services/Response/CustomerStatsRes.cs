namespace George.Services.Response;

/// <summary>CRM: Customer KPIs (matches frontend CustomerStatsRes).</summary>
public class CustomerStatsRes
{
    public int TotalCustomers { get; set; }
    public int? TotalCustomersTrendPercent { get; set; }
    public int ReturningCustomersPercent { get; set; }
    public int AverageReturnDays { get; set; }
    public decimal AverageOrdersPerCustomer { get; set; }
}
