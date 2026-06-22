using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — the WooCommerce product id for a product IN A SPECIFIC SITE's store. The same
/// product has a different Woo product id in each store, so it cannot be tracked by the single
/// Product.WooCommerceId column. One row per (product, site).
/// </summary>
[Index("ProductId", "SiteId", Name = "UX_ProductSiteWooId_Product_Site", IsUnique = true)]
public partial class ProductSiteWooId
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int SiteId { get; set; }

    public int WooCommerceProductId { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductSiteWooId")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
