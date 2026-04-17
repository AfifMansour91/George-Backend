namespace George.Services.Response
{
    public class IncomeReportRangeDto
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtcExclusive { get; set; }
    }

    public class IncomeReportCategoryOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class IncomeReportKpisDto
    {
        public decimal TotalIncome { get; set; }
        public decimal TotalIncomeUnfiltered { get; set; }
        public decimal TotalIncomeBaseline { get; set; }
        public decimal AvgOrder { get; set; }
        public decimal AvgOrderBaseline { get; set; }
        public decimal AvgItemsPerOrder { get; set; }
        public decimal AvgItemsPerOrderBaseline { get; set; }
        public decimal ReturningPct { get; set; }
        public decimal ReturningIncome { get; set; }
        public decimal NewIncome { get; set; }
    }

    public class IncomeReportDayRowDto
    {
        public string Date { get; set; } = "";
        public string Label { get; set; } = "";
        public decimal Income { get; set; }
        public decimal IncomeDayTotalUnfiltered { get; set; }
        public decimal? PctOfDay { get; set; }
        public int Orders { get; set; }
        public decimal DeliveryProductRevenue { get; set; }
        public int DeliveryOrders { get; set; }
        public decimal ShippingFees { get; set; }
        public decimal PickupProductRevenue { get; set; }
        public int PickupOrders { get; set; }
        public decimal Discounts { get; set; }
    }

    public class IncomeReportDayTotalsDto
    {
        public decimal Income { get; set; }
        public int Orders { get; set; }
        public decimal DeliveryProductRevenue { get; set; }
        public int DeliveryOrders { get; set; }
        public decimal ShippingFees { get; set; }
        public decimal PickupProductRevenue { get; set; }
        public int PickupOrders { get; set; }
        public decimal Discounts { get; set; }
    }

    public class IncomeReportOrderRowDto
    {
        public int OrderId { get; set; }
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string Source { get; set; } = "";
        public decimal Income { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public string DeliveryType { get; set; } = "";
    }

    public class IncomeReportSourceSliceDto
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Pct { get; set; }
        public decimal Income { get; set; }
    }

    public class IncomeReportSegmentsDto
    {
        public decimal DeliveryPct { get; set; }
        public decimal PickupPct { get; set; }
        public List<IncomeReportSourceSliceDto> SourceSlices { get; set; } = new();
        public string? PeakOrderHourLabel { get; set; }
        public string? PeakDeliveryHourLabel { get; set; }
    }

    public class IncomeReportTopProductDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string QuantityLabel { get; set; } = "";
        public decimal Revenue { get; set; }
        public bool TrendUp { get; set; }
    }

    public class IncomeReportRes
    {
        public IncomeReportRangeDto CurrentRange { get; set; } = new();
        public IncomeReportRangeDto? BaselineRange { get; set; }
        public List<IncomeReportCategoryOptionDto> Categories { get; set; } = new();
        public IncomeReportKpisDto Kpis { get; set; } = new();
        public List<IncomeReportDayRowDto> DayRows { get; set; } = new();
        public IncomeReportDayTotalsDto DayTotals { get; set; } = new();
        public List<IncomeReportOrderRowDto> OrderRows { get; set; } = new();
        public IncomeReportSegmentsDto Segments { get; set; } = new();
        public List<IncomeReportTopProductDto> TopProducts { get; set; } = new();
    }
}
