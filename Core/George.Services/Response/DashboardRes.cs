namespace George.Services.Response;

public class DashboardCountAmountDto
{
    public int Count { get; set; }
    public decimal Amount { get; set; }
    public List<string> PreviewNames { get; set; } = new();
}

public class DashboardKpiTrendDto
{
    public decimal? IncomePct { get; set; }
    public decimal? OrdersPct { get; set; }
    public decimal? AvgOrderPct { get; set; }
    public decimal? AvgItemsPct { get; set; }
}

public class DashboardKpisDto
{
    public decimal TotalIncome { get; set; }
    public int TotalOrders { get; set; }
    public decimal AvgOrder { get; set; }
    public decimal AvgItemsPerOrder { get; set; }
    public DashboardKpiTrendDto VsYesterday { get; set; } = new();
    public DashboardKpiTrendDto VsSameWeekday { get; set; } = new();
}

public class DashboardActiveOrdersDto
{
    public DashboardCountAmountDto New { get; set; } = new();
    public DashboardCountAmountDto InTreatment { get; set; } = new();
    public DashboardCountAmountDto Ready { get; set; } = new();
}

public class DashboardForwardProjectionDto
{
    public DashboardCountAmountDto TodayRemaining { get; set; } = new();
    public DashboardCountAmountDto Tomorrow { get; set; } = new();
    public DashboardCountAmountDto RestOfWeek { get; set; } = new();
    public DashboardCountAmountDto PipelineTotal { get; set; } = new();
}

public class DashboardAnomalyProductDto
{
    public string Name { get; set; } = "";
    public decimal PctAboveAvg { get; set; }
}

public class DashboardInsightsDto
{
    public decimal WeekdayAvgOrders { get; set; }
    public int TodayOrders { get; set; }
    public decimal TodayIncome { get; set; }
    public int ExpectedRemainingOrders { get; set; }
    public decimal? AvgPrepMinutes { get; set; }
    public decimal? AvgDeliveryMinutes { get; set; }
    public decimal? PerformanceVsWeekdayPct { get; set; }
    public int Next3HoursExpectedOrders { get; set; }
    public DashboardAnomalyProductDto? TopAnomalyProduct { get; set; }
}

public class DashboardSummaryRes
{
    public DashboardKpisDto Kpis { get; set; } = new();
    public DashboardActiveOrdersDto ActiveOrders { get; set; } = new();
    public DashboardForwardProjectionDto ForwardProjection { get; set; } = new();
    public DashboardInsightsDto Insights { get; set; } = new();
}

public class LiveActivityEventRes
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public int SiteId { get; set; }
    public string SiteName { get; set; } = "";
    public DateTime OccurredAt { get; set; }
    public string? EntityType { get; set; }
    public int? EntityId { get; set; }
    public string Severity { get; set; } = "normal";
}

public class DeliveryProductRowRes
{
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public decimal TotalQuantity { get; set; }
    public string QuantityLabel { get; set; } = "";
    public int OrderCount { get; set; }
}
