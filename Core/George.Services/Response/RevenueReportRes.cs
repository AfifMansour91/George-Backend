namespace George.Services.Response
{
    public class RevenueReportRangeDto
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtcExclusive { get; set; }
    }

    public class RevenueReportFilterOptionDto
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
    }

    public class RevenueReportCategoryOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class RevenueReportKpisDto
    {
        public decimal NetRevenue { get; set; }
        public decimal NetRevenueBaseline { get; set; }
        public int OrderCount { get; set; }
        public int OrderCountBaseline { get; set; }
        public decimal CreditsAmount { get; set; }
        public int CreditsPartialCount { get; set; }
        public int CreditsFullCount { get; set; }
        public decimal CreditsAmountBaseline { get; set; }
        public decimal CancellationsAmount { get; set; }
        public decimal CancellationsOrderPct { get; set; }
        public decimal CancellationsAmountBaseline { get; set; }
    }

    public class RevenueReportPipelineDto
    {
        public int PendingChargeCount { get; set; }
        public decimal PendingChargeAmount { get; set; }
        public decimal AvgDaysToCharge { get; set; }
    }

    public class RevenueReportTrendPointDto
    {
        public string Date { get; set; } = "";
        public string Label { get; set; } = "";
        public decimal Income { get; set; }
    }

    public class RevenueReportDayRowDto
    {
        public string Date { get; set; } = "";
        public string Label { get; set; } = "";
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal Credits { get; set; }
        public decimal Cancellations { get; set; }
        public decimal Discounts { get; set; }
    }

    public class RevenueReportDayTotalsDto
    {
        public int Orders { get; set; }
        public decimal Revenue { get; set; }
        public decimal Credits { get; set; }
        public decimal Cancellations { get; set; }
        public decimal Discounts { get; set; }
    }

    public class RevenueReportOrderRowDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string Source { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public string Status { get; set; } = "";
        public string? StatusReason { get; set; }
        public decimal Total { get; set; }
        public string? InvoiceUrl { get; set; }
        public string? RefundInvoiceUrl { get; set; }
        public bool IsCancelled { get; set; }
    }

    public class RevenueReportSliceDto
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Income { get; set; }
        public int OrderCount { get; set; }
        public decimal Pct { get; set; }
    }

    public class RevenueReportCitySlicesDto
    {
        public List<RevenueReportSliceDto> Top { get; set; } = new();
        public int MoreCount { get; set; }
    }

    public class RevenueReportSegmentsDto
    {
        public List<RevenueReportSliceDto> PaymentSlices { get; set; } = new();
        public List<RevenueReportSliceDto> ChannelSlices { get; set; } = new();
        public RevenueReportCitySlicesDto CitySlices { get; set; } = new();
        public List<RevenueReportSliceDto> CategorySlices { get; set; } = new();
        public int CategoryMoreCount { get; set; }
    }

    public class RevenueReportRes
    {
        public RevenueReportRangeDto CurrentRange { get; set; } = new();
        public RevenueReportRangeDto? BaselineRange { get; set; }
        public string Grouping { get; set; } = "daily";
        public List<RevenueReportCategoryOptionDto> Categories { get; set; } = new();
        public List<RevenueReportFilterOptionDto> Cities { get; set; } = new();
        public List<RevenueReportFilterOptionDto> Channels { get; set; } = new();
        public List<RevenueReportFilterOptionDto> PaymentMethods { get; set; } = new();
        public List<RevenueReportFilterOptionDto> Statuses { get; set; } = new();
        public RevenueReportKpisDto Kpis { get; set; } = new();
        public RevenueReportPipelineDto? Pipeline { get; set; }
        public List<RevenueReportTrendPointDto> TrendPoints { get; set; } = new();
        public List<RevenueReportTrendPointDto> BaselineTrendPoints { get; set; } = new();
        public List<RevenueReportDayRowDto> DayRows { get; set; } = new();
        public RevenueReportDayTotalsDto DayTotals { get; set; } = new();
        public List<RevenueReportOrderRowDto> OrderRows { get; set; } = new();
        public RevenueReportSegmentsDto Segments { get; set; } = new();
    }
}
