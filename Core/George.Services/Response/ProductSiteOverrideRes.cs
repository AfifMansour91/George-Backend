using System;
using System.Collections.Generic;

namespace George.Services.Response
{
    /// <summary>MultiSite Phase 2 — a per-(product, site) override row.</summary>
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
}
