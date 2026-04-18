namespace George.Services.Response
{
    public class QuantityConcentrationRangeDto
    {
        /// <summary>Local calendar start <c>yyyy-MM-dd</c> (same semantics as SPA query).</summary>
        public string FromLocal { get; set; } = "";

        /// <summary>Inclusive end date <c>yyyy-MM-dd</c>.</summary>
        public string ToLocal { get; set; } = "";
    }

    public class QuantityConcentrationCategoryOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class QuantityConcentrationLineDto
    {
        public string LineLabel { get; set; } = "";
        public decimal? WeightPerUnitKg { get; set; }
        public decimal? QuantityKg { get; set; }
        public decimal? QuantityUnits { get; set; }
        public string? Note { get; set; }
    }

    public class QuantityConcentrationProductGroupDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int CategoryId { get; set; }
        public decimal? TotalQuantityKg { get; set; }
        public decimal? TotalQuantityUnits { get; set; }
        public decimal? StockKg { get; set; }
        public decimal? StockUnits { get; set; }
        /// <summary>ok | low | out — aligned with products report / SPA buckets.</summary>
        public string StockStatus { get; set; } = "ok";
        public decimal? ShortageKg { get; set; }
        public decimal? ShortageUnits { get; set; }
        public List<QuantityConcentrationLineDto> Lines { get; set; } = new();
    }

    public class QuantityConcentrationReportRes
    {
        public QuantityConcentrationRangeDto DeliveryRange { get; set; } = new();
        public List<QuantityConcentrationCategoryOptionDto> Categories { get; set; } = new();
        public List<QuantityConcentrationProductGroupDto> ProductGroups { get; set; } = new();
    }
}
