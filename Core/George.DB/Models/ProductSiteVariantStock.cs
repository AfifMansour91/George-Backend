using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 - per-(variant, site) override row. Originally per-site stock; extended to also hold
/// the per-site variant price/sale price and an exclusion flag (a variant "deleted" in one branch is hidden
/// there, not removed from the canonical product). Absence of a row / null field means inherit canonical.
/// </summary>
[Index("ProductVariantId", "SiteId", Name = "UX_ProductSiteVariantStock_Variant_Site", IsUnique = true)]
[Index("SiteId", Name = "IX_ProductSiteVariantStock_SiteId")]
[Index("ProductId", Name = "IX_ProductSiteVariantStock_ProductId")]
public partial class ProductSiteVariantStock
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int ProductVariantId { get; set; }

    public int SiteId { get; set; }

    /// <summary>Denormalized for convenient per-product/site filtering.</summary>
    public int? ProductId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? StockQuantity { get; set; }

    public int? StockStatusId { get; set; }

    /// <summary>Per-site regular price for this variant (null = inherit canonical variant price).</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Price { get; set; }

    /// <summary>Per-site sale price for this variant (null = inherit canonical variant sale price).</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SalePrice { get; set; }

    /// <summary>When true, this variant is hidden/removed for this site only (canonical keeps it).</summary>
    public bool IsExcluded { get; set; }

    [ForeignKey("ProductVariantId")]
    [InverseProperty("ProductSiteVariantStock")]
    public virtual ProductVariant ProductVariant { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
