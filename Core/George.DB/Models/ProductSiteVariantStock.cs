using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — per-(variant, site) stock. Holds the stock of a product variant for a given
/// site (stock is always per-site). Absence of a row means the site inherits the canonical variant stock.
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

    [ForeignKey("ProductVariantId")]
    [InverseProperty("ProductSiteVariantStock")]
    public virtual ProductVariant ProductVariant { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
