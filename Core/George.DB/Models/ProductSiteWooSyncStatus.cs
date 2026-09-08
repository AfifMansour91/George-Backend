using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Outcome of the LAST WooCommerce sync of a product to one site's store. Written by every product
/// sync (save-triggered or sync-all) so the product page can tell the shop that its change never
/// reached the store - before this, a failed sync was only a log line and the shop found out from
/// customers (Meshek Basar PT 8/9: "עוף טחון" in stock in George, out of stock on the site for a week).
/// One row per (product, site); overwritten on each attempt.
/// </summary>
[Index("ProductId", "SiteId", Name = "UX_ProductSiteWooSyncStatus_Product_Site", IsUnique = true)]
public partial class ProductSiteWooSyncStatus
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    public int SiteId { get; set; }

    public DateTime LastSyncAt { get; set; }

    public bool Success { get; set; }

    /// <summary>"created" | "updated" | "adopted" on success; null on failure.</summary>
    [StringLength(20)]
    public string? Action { get; set; }

    public int? WooCommerceProductId { get; set; }

    /// <summary>User-facing error of the last FAILED attempt; null after a success.</summary>
    [StringLength(1000)]
    public string? Error { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("SiteId")]
    public virtual Site Site { get; set; } = null!;
}
