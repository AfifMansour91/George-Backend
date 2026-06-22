using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — per-(product, site) override layer. A sparse row that holds only the
/// fields that differ from the canonical Product for a given site, plus exclusion + per-site stock.
/// Absence of a row means the site fully inherits the canonical product (with default stock).
/// </summary>
[Index("ProductId", "SiteId", Name = "UX_ProductSiteOverride_Product_Site", IsUnique = true)]
[Index("SiteId", Name = "IX_ProductSiteOverride_SiteId")]
[Index("AccountId", Name = "IX_ProductSiteOverride_AccountId")]
public partial class ProductSiteOverride
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int ProductId { get; set; }

    public int SiteId { get; set; }

    /// <summary>Denormalized for tenancy filtering; matches the product's account.</summary>
    public int? AccountId { get; set; }

    /// <summary>True = product excluded from network management at this site (managed locally there).</summary>
    public bool IsExcluded { get; set; }

    // --- Per-site scalar field overrides (Phase 2: full per-site editing of scalar fields) ---
    [StringLength(300)]
    public string? Name { get; set; }

    [StringLength(2000)]
    public string? ShortDescription { get; set; }

    public string? LongDescription { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Weight { get; set; }

    [StringLength(5)]
    public string? WeightUnit { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    [StringLength(300)]
    public string? SeoTitle { get; set; }

    [StringLength(2000)]
    public string? SeoDescription { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Price { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SalePrice { get; set; }

    [Precision(0)]
    public DateTime? SalePriceStartDate { get; set; }

    [Precision(0)]
    public DateTime? SalePriceEndDate { get; set; }

    /// <summary>Available/unavailable at this site (storefront visibility), independent of stock.</summary>
    public bool? Availability { get; set; }

    // --- Per-site inventory (Phase 2: stock is always per-site) ---
    public int? StockManagementTypeId { get; set; }

    public int? StockStatusId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? StockQuantity { get; set; }

    public bool? VariationStockByQuantity { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? LowStockThreshold { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductSiteOverride")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
