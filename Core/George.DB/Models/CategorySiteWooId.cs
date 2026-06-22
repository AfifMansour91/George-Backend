using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — the WooCommerce category id for a category IN A SPECIFIC SITE's store. A category
/// shared across sites (network mode) has a different Woo category id in each store, so it cannot be tracked
/// by the single Category.WooCommerceId column (which would point at one store and corrupt the other on sync).
/// One row per (category, site). Only populated/consulted for network-managed accounts.
/// </summary>
[Index("CategoryId", "SiteId", Name = "UX_CategorySiteWooId_Category_Site", IsUnique = true)]
public partial class CategorySiteWooId
{
    [Key]
    public int Id { get; set; }

    public int CategoryId { get; set; }

    public int SiteId { get; set; }

    public int WooCommerceCategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
