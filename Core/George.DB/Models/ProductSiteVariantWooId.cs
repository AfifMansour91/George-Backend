using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — the WooCommerce VARIATION id for a product variant IN A SPECIFIC SITE's store. The same
/// variant has a different Woo variation id in each store, so it cannot be tracked by the single
/// ProductVariant.WooCommerceVariationId column: a multi-site variable product's second site reused the first
/// site's variation ids → PUT 404 → recreate → the recreated variation was then wrongly deleted as an orphan,
/// leaving the second store with zero variations (a variable product with no variations shows out of stock).
/// One row per (variant, site). Mirrors <see cref="ProductSiteWooId"/> for the parent product.
/// </summary>
[Index("ProductVariantId", "SiteId", Name = "UX_ProductSiteVariantWooId_Variant_Site", IsUnique = true)]
public partial class ProductSiteVariantWooId
{
    [Key]
    public int Id { get; set; }

    public int ProductVariantId { get; set; }

    public int SiteId { get; set; }

    /// <summary>The owning product, denormalized so all of a product's per-site variation ids load in one query.</summary>
    public int ProductId { get; set; }

    public int WooCommerceVariationId { get; set; }

    [ForeignKey("ProductVariantId")]
    [InverseProperty("ProductSiteVariantWooId")]
    public virtual ProductVariant ProductVariant { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
