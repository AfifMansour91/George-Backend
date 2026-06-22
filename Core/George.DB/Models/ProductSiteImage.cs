using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// MultiSite Phase 2 — per-(product, site) image. When a product has any rows here for a given site,
/// they REPLACE the canonical ProductImage list for that site (effective view). Absence = inherit canonical.
/// </summary>
[Index("ProductId", "SiteId", Name = "IX_ProductSiteImage_Product_Site")]
public partial class ProductSiteImage
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int SiteId { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    public int SortOrder { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductSiteImage")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
