namespace George.Services.Response
{
    public class ProductBundleSwapRes
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public string? ProductName { get; set; }
        public string? Sku { get; set; }
        public decimal Surcharge { get; set; }
        public int SortOrder { get; set; }
        /// <summary>The swap's catalog product was soft-deleted after the bundle was defined; the bundle needs attention.</summary>
        public bool ProductDeleted { get; set; }
    }

    public class ProductBundleComponentRes
    {
        public int Id { get; set; }
        /// <summary>Stable key sent to Woo verbatim ("c" + id).</summary>
        public string Key { get; set; } = string.Empty;
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public string? ProductName { get; set; }
        public string? Sku { get; set; }
        /// <summary>Per bundle, in the component's own unit.</summary>
        public decimal Qty { get; set; }
        public int SortOrder { get; set; }
        public bool Swappable { get; set; }
        public string? Description { get; set; }
        /// <summary>As reported back by Woo (kg | unit); null until the first successful sync.</summary>
        public string? Unit { get; set; }
        /// <summary>As reported back by Woo (weight | unit ...).</summary>
        public string? Mode { get; set; }
        public decimal? UnitWeightKg { get; set; }
        /// <summary>The component's catalog product was soft-deleted after the bundle was defined (price excludes it, sync refuses).</summary>
        public bool ProductDeleted { get; set; }
        public List<ProductBundleSwapRes> Swaps { get; set; } = new();
    }

    /// <summary><c>ProductRes.Bundle</c> - BUNDLES_SYNC_SPEC.md §3.1.</summary>
    public class ProductBundleRes
    {
        public string PricingMode { get; set; } = "fixed";
        public decimal? FixedPrice { get; set; }
        public string DiscountType { get; set; } = "none";
        public decimal DiscountValue { get; set; }
        public string OosBehavior { get; set; } = "unavailable";
        public string Layout { get; set; } = "grid";
        public string CartDisplay { get; set; } = "name_with_components";
        public string InvoiceDisplay { get; set; } = "bundle";
        public bool HidePriceLabels { get; set; }
        public bool ReweighPrice { get; set; }
        public bool ShowComponentsInDesc { get; set; }
        /// <summary>George-side pricing engine over catalog (or site-effective) prices: base + no surcharges.</summary>
        public decimal ComputedPrice { get; set; }
        public decimal ComputedBasePrice { get; set; }
        public decimal ComputedRawPrice { get; set; }
        public List<ProductBundleComponentRes> Components { get; set; } = new();
    }

    /// <summary>GET /Bundle list row - spec §3.2.</summary>
    public class BundleListItemRes
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public string? Status { get; set; }
        public string? ImageUrl { get; set; }
        public string PricingMode { get; set; } = "fixed";
        public decimal ComputedPrice { get; set; }
        public int ComponentsCount { get; set; }
        /// <summary>At least one component or swap points at a deleted catalog product.</summary>
        public bool HasDeletedComponent { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public List<ProductSiteWooSyncStatusRes> WooSyncStatuses { get; set; } = new();
    }

    public class BundleListRes
    {
        public List<BundleListItemRes> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int Total { get; set; }
    }

    public class BundleAvailabilityComponentRes
    {
        public int? Index { get; set; }
        public string? Key { get; set; }
        public int? ProductId { get; set; }
        public int? VariationId { get; set; }
        public string? Name { get; set; }
        /// <summary>Configured quantity per bundle, in <see cref="Unit"/>.</summary>
        public decimal? Qty { get; set; }
        public bool? InStock { get; set; }
        public decimal? AvailableQuantity { get; set; }
        public string? Unit { get; set; }
        public string? Mode { get; set; }
    }

    /// <summary>GET /Bundle/{productId}/availability - proxied from OC Bundles <c>GET /bundles/{id}/availability</c>.</summary>
    public class BundleAvailabilityRes
    {
        public int WooProductId { get; set; }
        public decimal? Price { get; set; }
        public decimal? AvailableQuantity { get; set; }
        public bool? InStock { get; set; }
        public List<BundleAvailabilityComponentRes> Components { get; set; } = new();
        /// <summary>The plugin's raw JSON, for fields this DTO does not model.</summary>
        public string? Raw { get; set; }
    }

    public class BundlePriceComponentRes
    {
        public int ComponentId { get; set; }
        public int ProductId { get; set; }
        public int? ProductVariantId { get; set; }
        public decimal Qty { get; set; }
        public decimal LineQty { get; set; }
        public decimal Surcharge { get; set; }
        public decimal? Share { get; set; }
    }

    /// <summary>POST /Bundle/price - spec §3.2.</summary>
    public class BundlePriceRes
    {
        public decimal UnitPrice { get; set; }
        public decimal BasePrice { get; set; }
        public decimal RawPrice { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal SurchargeTotal { get; set; }
        public decimal LineTotal { get; set; }
        public List<BundlePriceComponentRes> Components { get; set; } = new();
    }

    /// <summary>POST /Bundle/{productId}/sync result.</summary>
    public class BundleSyncRes
    {
        public int ProductId { get; set; }
        public int SiteId { get; set; }
        public bool Success { get; set; }
        public int? WooCommerceProductId { get; set; }
        public string? Action { get; set; }
        public string? Error { get; set; }
    }
}
