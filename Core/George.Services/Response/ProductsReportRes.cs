namespace George.Services.Response
{
    public class ProductsReportRangeDto
    {
        public DateTime FromUtc { get; set; }
        public DateTime ToUtcExclusive { get; set; }
    }

    public class ProductsReportCategoryOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class ProductsReportKpisDto
    {
        public int DistinctProductsSold { get; set; }
        public int CatalogProductCount { get; set; }
        /// <summary>Catalog products with no qualifying sales line in the selected period (and category filter).</summary>
        public int UnsoldInPeriodCount { get; set; }
        public string? LeadingCategoryName { get; set; }
        public decimal? LeadingCategoryRevenuePct { get; set; }
        public int OutOfStockCount { get; set; }
        public int LowStockCount { get; set; }
        /// <summary>Among out-of-stock catalog products: min days since last paid completed sale (most recent).</summary>
        public int? DaysSinceLastSaleAmongOutOfStock { get; set; }
        /// <summary>Among low-stock catalog products: min days since last paid completed sale (most recent).</summary>
        public int? DaysSinceLastSaleAmongLowStock { get; set; }
    }

    public class ProductsReportUnsoldRowDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        /// <summary>Days from last paid completed sale to UTC &quot;today&quot; when the report runs; null if never sold.</summary>
        public int? DaysSinceLastSale { get; set; }
        /// <summary>ok | low | out</summary>
        public string StockStatus { get; set; } = "ok";
        public decimal? StockQuantity { get; set; }
        public bool IsWeighted { get; set; }
    }

    public class ProductsReportCutRowDto
    {
        public string CutLabel { get; set; } = "";
        public decimal? QuantityKg { get; set; }
        public decimal? QuantityUnits { get; set; }
        public decimal Revenue { get; set; }
        /// <summary>ok | low | out — per variation when resolvable, else parent rollup.</summary>
        public string StockStatus { get; set; } = "ok";
    }

    public class ProductsReportProductRowDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public string CategoryName { get; set; } = "";
        public int? CategoryId { get; set; }
        public string? ImageUrl { get; set; }
        public decimal? QuantityKg { get; set; }
        public decimal? QuantityUnits { get; set; }
        public decimal Revenue { get; set; }
        public decimal? TrendPct { get; set; }
        public bool? TrendUp { get; set; }
        /// <summary>ok | low | out</summary>
        public string StockStatus { get; set; } = "ok";
        public List<ProductsReportCutRowDto> CutRows { get; set; } = new();
    }

    public class ProductsReportCategorySliceDto
    {
        public int? CategoryId { get; set; }
        public string Name { get; set; } = "";
        public string? Color { get; set; }
        public decimal Pct { get; set; }
        public decimal Revenue { get; set; }
        /// <summary>Optional breakdown by direct child categories (revenue roll-up from leaf sales).</summary>
        public List<ProductsReportCategorySliceDto>? SubSlices { get; set; }
    }

    public class ProductsReportUpsellPairDto
    {
        public int ProductAId { get; set; }
        public string ProductAName { get; set; } = "";
        public string? ProductAImageUrl { get; set; }
        public int ProductBId { get; set; }
        public string ProductBName { get; set; } = "";
        public string? ProductBImageUrl { get; set; }
        public decimal OrdersPct { get; set; }
        public decimal? BundleRevenue { get; set; }
    }

    public class ProductsReportOptionRankDto
    {
        public int Rank { get; set; }
        public string OptionLabel { get; set; } = "";
        public decimal Revenue { get; set; }
        public string? QuantityLabel { get; set; }
    }

    public class ProductsReportRes
    {
        public ProductsReportRangeDto CurrentRange { get; set; } = new();
        public List<ProductsReportCategoryOptionDto> Categories { get; set; } = new();
        public List<ProductsReportCategoryOptionDto> Suppliers { get; set; } = new();
        public List<ProductsReportCategoryOptionDto> Brands { get; set; } = new();
        /// <summary>Distinct cut / option labels in the period (for table filter).</summary>
        public List<string> CutOptions { get; set; } = new();
        public ProductsReportKpisDto Kpis { get; set; } = new();
        public List<ProductsReportProductRowDto> ProductRows { get; set; } = new();
        public List<ProductsReportCategorySliceDto> CategorySlices { get; set; } = new();
        public List<ProductsReportUpsellPairDto> UpsellPairs { get; set; } = new();
        public List<ProductsReportOptionRankDto> TopOptions { get; set; } = new();

        /// <summary>Subset of unsold catalog products for the modal (server-capped).</summary>
        public List<ProductsReportUnsoldRowDto> UnsoldProducts { get; set; } = new();
    }
}
