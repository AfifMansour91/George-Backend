using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — per-(product, site) category assignment. When a product has any rows here for a
/// given site, those categories REPLACE the canonical ProductCategory assignment for that site (effective
/// view). Absence of rows for a (product, site) means the site inherits the canonical categories.
/// </summary>
[Index("ProductId", "SiteId", Name = "IX_ProductSiteCategory_Product_Site")]
[Index("ProductId", "SiteId", "CategoryId", Name = "UX_ProductSiteCategory_Product_Site_Category", IsUnique = true)]
public partial class ProductSiteCategory
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int SiteId { get; set; }

    public int CategoryId { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductSiteCategory")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;

    [ForeignKey("CategoryId")]
    public virtual Category Category { get; set; } = null!;
}
