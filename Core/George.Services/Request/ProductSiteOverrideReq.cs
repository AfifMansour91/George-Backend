using System;
using System.Collections.Generic;

namespace George.Services.Request
{
    /// <summary>
    /// MultiSite Phase 2 - upsert a per-(product, site) override. Only the provided fields are written;
    /// omitted fields keep their current override value (use the reset endpoint to clear back to canonical).
    /// </summary>
    public class ProductSiteOverrideReq
    {
        public bool? IsExcluded { get; set; }
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? LongDescription { get; set; }
        public decimal? Weight { get; set; }
        public string? WeightUnit { get; set; }
        public string? Sku { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
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

    /// <summary>Body for POST /Product/{id}/exclude and /include.</summary>
    public class ProductSiteScopeReq
    {
        public int SiteId { get; set; }
        /// <summary>For include: also reset existing override fields to inherit the canonical.</summary>
        public bool ResetFields { get; set; }
    }

    public class ProductSiteVariantStockItemReq
    {
        public int VariantId { get; set; }
        public decimal? StockQuantity { get; set; }
        public string? StockStatus { get; set; }
    }

    /// <summary>Body for PUT /Product/{id}/variant-stock.</summary>
    public class ProductSiteVariantStockReq
    {
        public int SiteId { get; set; }
        public List<ProductSiteVariantStockItemReq> Items { get; set; } = new();
    }
}
