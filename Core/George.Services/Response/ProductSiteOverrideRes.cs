using System;
using System.Collections.Generic;

namespace George.Services.Response
{
    /// <summary>MultiSite Phase 2 - a per-(product, site) override row.</summary>
    public class ProductSiteOverrideRes
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public int SiteId { get; set; }
        public int? AccountId { get; set; }
        public bool IsExcluded { get; set; }
        public decimal? Price { get; set; }
        public decimal? SalePrice { get; set; }
        public DateTime? SalePriceStartDate { get; set; }
        public DateTime? SalePriceEndDate { get; set; }
        public bool? Availability { get; set; }
        public string? StockManagementType { get; set; }
        public string? StockStatus { get; set; }
        public decimal? StockQuantity { get; set; }
        public bool? VariationStockByQuantity { get; set; }
        public decimal? LowStockThreshold { get; set; }
    }

    /// <summary>Reason a product appears in the local/excluded list for a site.</summary>
    public class LocalProductEntryRes
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = null!;
        public int SiteId { get; set; }
        public string Reason { get; set; } = "local"; // "local" | "excluded"
    }

    public class LocalProductsRes
    {
        public List<LocalProductEntryRes> Items { get; set; } = new();
        public int Total { get; set; }
    }

    /// <summary>Which of price/sku/stock a product has a per-site override on (drives the "הצג" / Show affordance).</summary>
    public class ProductFieldOverrideFlagsRes
    {
        public int ProductId { get; set; }
        public bool PriceOverridden { get; set; }
        public bool SkuOverridden { get; set; }
        public bool StockOverridden { get; set; }
    }

    /// <summary>One site's effective price/sku/stock for a product, with per-field override flags.</summary>
    public class SiteFieldValueRes
    {
        public int SiteId { get; set; }
        public string SiteName { get; set; } = "";
        public decimal? Price { get; set; }
        public string? Sku { get; set; }
        public decimal? StockQuantity { get; set; }
        public bool PriceOverridden { get; set; }
        public bool SkuOverridden { get; set; }
        public bool StockOverridden { get; set; }
    }

    /// <summary>Per-site price/sku/stock for a product (for the per-branch edit popup), plus the canonical base values.</summary>
    public class ProductSiteFieldValuesRes
    {
        public int ProductId { get; set; }
        public decimal? BasePrice { get; set; }
        public string? BaseSku { get; set; }
        public decimal? BaseStock { get; set; }
        public List<SiteFieldValueRes> Sites { get; set; } = new();
    }
}
