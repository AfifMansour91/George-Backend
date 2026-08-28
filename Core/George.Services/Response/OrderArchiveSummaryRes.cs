namespace George.Services.Response;

/// <summary>Archive KPI summary over the whole filtered period (GET /Order/ArchiveSummary) - stable across table paging/filters.</summary>
public class OrderArchiveSummaryRes
{
    public int Total { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    /// <summary>Orders a credit (full or partial) was issued for.</summary>
    public int Credited { get; set; }
    /// <summary>Total amount credited back across the period.</summary>
    public decimal CreditedSum { get; set; }
    /// <summary>Distinct delivery cities in the period (for the city filter options).</summary>
    public List<string> Cities { get; set; } = new();
    /// <summary>True when the period contains orders without a delivery city.</summary>
    public bool HasCityNone { get; set; }
}
