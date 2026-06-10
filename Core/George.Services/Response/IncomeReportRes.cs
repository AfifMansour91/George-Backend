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

    public class IncomeReportCityOptionDto
    {
        /// <summary>Filter query value; <c>__no_city__</c> for orders without <see cref="George.DB.Order.DeliveryCity"/>.</summary>
        public string Key { get; set; } = "";
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
        /// <summary>Store-facing order number (WooCommerce / manual); use <see cref="OrderId"/> for internal links.</summary>
        public string OrderNumber { get; set; } = "";
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = "";
        public string Source { get; set; } = "";
        /// <summary>Merchandise (line totals) attributed to this row — אפיון עמודת "מוצרים".</summary>
        public decimal ProductRevenue { get; set; }
        /// <summary>סה״כ לתשלום (כמו Order.Total) לפי הקצאת קטגוריה.</summary>
        public decimal Income { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Discount { get; set; }
        public string DeliveryType { get; set; } = "";
        /// <summary>קוד קופון אם זוהה ב-JSON / הערות (אין עמודה ייעודית ב-DB).</summary>
        public string CouponCode { get; set; } = "";
    }

    public class IncomeReportSourceSliceDto
    {
        public string Key { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Pct { get; set; }
        public decimal Income { get; set; }
        /// <summary>Count of orders attributed to this source (for income vs orders toggle in UI).</summary>
        public int OrderCount { get; set; }
    }

    public class IncomeReportHourBucketDto
    {
        public int Hour { get; set; }
        public string Label { get; set; } = "";
        public int OrderCount { get; set; }
        public decimal PctOfTotal { get; set; }
    }

    public class IncomeReportSegmentsDto
    {
        public decimal DeliveryPct { get; set; }
        public decimal PickupPct { get; set; }
        /// <summary>סה״כ הכנסות (Order.Total לפי הקטגוריה) להזמנות משלוח.</summary>
        public decimal DeliveryIncome { get; set; }
        /// <summary>סה״כ הכנסות לאיסוף עצמי.</summary>
        public decimal PickupIncome { get; set; }
        /// <summary>הזמנות משלוח (ספירת הזמנות, לא לפי הכנסה).</summary>
        public int DeliveryOrderCount { get; set; }
        /// <summary>הזמנות איסוף עצמי.</summary>
        public int PickupOrderCount { get; set; }
        public List<IncomeReportSourceSliceDto> SourceSlices { get; set; } = new();
        public string? PeakOrderHourLabel { get; set; }
        public string? PeakDeliveryHourLabel { get; set; }
        /// <summary>פילוח הזמנות לפי שעת יצירה (למודאל "לפרטים נוספים").</summary>
        public List<IncomeReportHourBucketDto> OrderHours { get; set; } = new();
        /// <summary>פילוח לפי שעת אספקה מתוכננת (כשיש DeliveryDate).</summary>
        public List<IncomeReportHourBucketDto> DeliveryHours { get; set; } = new();
    }

    public class IncomeReportTopProductDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public string QuantityLabel { get; set; } = "";
        /// <summary>Catalog weighable product (by_weight / by_unit / by_unit_and_weight or IsWeighted).</summary>
        public bool IsWeighted { get; set; }
        /// <summary>Optional: kg sold when line is weight-based (picked preferred).</summary>
        public decimal? QuantityKg { get; set; }
        /// <summary>Optional: unit count when not purely weight.</summary>
        public decimal? QuantityUnits { get; set; }
        public decimal Revenue { get; set; }
        public bool TrendUp { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class IncomeReportRes
    {
        public IncomeReportRangeDto CurrentRange { get; set; } = new();
        public IncomeReportRangeDto? BaselineRange { get; set; }
        public List<IncomeReportCategoryOptionDto> Categories { get; set; } = new();
        /// <summary>Distinct delivery cities in the current period (before city filter).</summary>
        public List<IncomeReportCityOptionDto> Cities { get; set; } = new();
        public IncomeReportKpisDto Kpis { get; set; } = new();
        public List<IncomeReportDayRowDto> DayRows { get; set; } = new();
        public IncomeReportDayTotalsDto DayTotals { get; set; } = new();
        public List<IncomeReportOrderRowDto> OrderRows { get; set; } = new();
        public IncomeReportSegmentsDto Segments { get; set; } = new();
        public List<IncomeReportTopProductDto> TopProducts { get; set; } = new();
    }
}
