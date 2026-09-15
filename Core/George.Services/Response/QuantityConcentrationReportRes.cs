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
        public bool ShowUnitsInTotalQuantity { get; set; } = true;
        public string? Note { get; set; }
        /// <summary>Distinct orders contributing to this line (prep breakdown: "3 הזמנות").</summary>
        public int? OrderCount { get; set; }
        /// <summary>Live variant stock (units) when parent uses variation quantity management.</summary>
        public decimal? StockUnits { get; set; }
        public decimal? StockKg { get; set; }
        /// <summary>Catalog variant id when line maps to a variation (stock row).</summary>
        public int? VariantId { get; set; }
        /// <summary>variant | weightChoice | noteBucket | remainder - SPA rendering hint.</summary>
        public string? LineDisplayKind { get; set; }
        /// <summary>Display suffix for line stock when it differs from the parent group.</summary>
        public string? StockUnitLabel { get; set; }
    }

    public class QuantityConcentrationProductGroupDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int CategoryId { get; set; }
        public decimal? TotalQuantityKg { get; set; }
        public decimal? TotalQuantityUnits { get; set; }
        /// <summary>Part of <see cref="TotalQuantityKg"/> that came from bundle (מארז) child lines; 0 when none.</summary>
        public decimal FromBundlesKg { get; set; }
        /// <summary>Part of <see cref="TotalQuantityUnits"/> that came from bundle (מארז) child lines; 0 when none.</summary>
        public decimal FromBundlesUnits { get; set; }
        /// <summary>When false, total quantity column shows kg only (pure by-weight products).</summary>
        public bool ShowUnitsInTotalQuantity { get; set; } = true;
        /// <summary>When true, SPA shows optional unit-weight column for this product group.</summary>
        public bool ShowWeightPerUnitColumn { get; set; }
        /// <summary>Parent-row unit weight (kg) for weighted no-variation products (e.g. צלעות טלה) - from order lines, else catalog config.</summary>
        public decimal? WeightPerUnitKg { get; set; }
        public decimal? StockKg { get; set; }
        public decimal? StockUnits { get; set; }
        /// <summary><c>quantity</c> = numeric on-hand; <c>status</c> = in/out only (no shortage numbers).</summary>
        public string StockDisplayMode { get; set; } = "quantity";
        /// <summary>Display suffix for numeric stock, e.g. יח׳ or ק״ג (aligned with My Products).</summary>
        public string? StockUnitLabel { get; set; }
        public string? StockManagementType { get; set; }
        /// <summary>When <see cref="StockManagementType"/> is variation - per-variant numeric stock vs in/out only.</summary>
        public bool VariationStockByQuantity { get; set; }
        /// <summary>ok | low | out - aligned with products report / SPA buckets.</summary>
        public string StockStatus { get; set; } = "ok";
        public decimal? ShortageKg { get; set; }
        public decimal? ShortageUnits { get; set; }
        public List<QuantityConcentrationLineDto> Lines { get; set; } = new();
    }

    /// <summary>Component demand of one bundle product, aggregated from the child lines (BUNDLES_SYNC_SPEC.md §8).</summary>
    public class QuantityConcentrationBundleComponentDto
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public decimal QuantityKg { get; set; }
        public decimal QuantityUnits { get; set; }
    }

    public class QuantityConcentrationBundleDto
    {
        public int BundleProductId { get; set; }
        public string Name { get; set; } = "";
        /// <summary>Σ parent-line quantity (number of bundles) in the report's orders.</summary>
        public decimal OrderedBundles { get; set; }
        /// <summary>Bundles whose parent line (or every child line) was confirmed in picking.</summary>
        public decimal PickedBundles { get; set; }
        public int OrdersCount { get; set; }
        public List<QuantityConcentrationBundleComponentDto> Components { get; set; } = new();
    }

    public class QuantityConcentrationReportRes
    {
        public QuantityConcentrationRangeDto DeliveryRange { get; set; } = new();
        public List<QuantityConcentrationCategoryOptionDto> Categories { get; set; } = new();
        public List<QuantityConcentrationProductGroupDto> ProductGroups { get; set; } = new();

        /// <summary>
        /// Bundles (מארזים) ordered in the range - only with query <c>includeBundles=true</c> and
        /// <c>Account.BundlesEnabled</c>; null otherwise (UI hides the section).
        /// </summary>
        public List<QuantityConcentrationBundleDto>? Bundles { get; set; }
    }
}
