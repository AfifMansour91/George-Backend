using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One component slot of a bundle product: which catalog product (and optionally variant) goes in,
/// how much per bundle, and whether the customer/picker may swap it. Soft-deleted rows keep their
/// ids because order lines reference the slot (<c>OrderItem.BundleComponentId</c>).
/// Spec: BUNDLES_SYNC_SPEC.md §2.
/// </summary>
[Index("BundleProductId", "IsDeleted", Name = "IX_ProductBundleComponent_Bundle_IsDeleted")]
public partial class ProductBundleComponent
{
    [Key]
    public int Id { get; set; }

    public int BundleProductId { get; set; }

    public int ComponentProductId { get; set; }

    public int? ComponentVariantId { get; set; }

    /// <summary>Quantity per bundle in the component's own unit (kg for weight products, units otherwise).</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal Qty { get; set; }

    public int SortOrder { get; set; }

    public bool Swappable { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Stable key sent to Woo verbatim ("c" + Id, set after insert). Spec §5.3.</summary>
    [StringLength(40)]
    public string ComponentKey { get; set; } = string.Empty;

    /// <summary>Read-only mirror of what OC Bundles reported back (kg | unit).</summary>
    [StringLength(10)]
    public string? Unit { get; set; }

    /// <summary>Read-only mirror of what OC Bundles reported back (weight | unit | ...).</summary>
    [StringLength(20)]
    public string? Mode { get; set; }

    /// <summary>Read-only mirror of what OC Bundles reported back.</summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? UnitWeightKg { get; set; }

    public bool IsDeleted { get; set; }

    [ForeignKey("BundleProductId")]
    [InverseProperty("BundleComponents")]
    public virtual Product BundleProduct { get; set; } = null!;

    [ForeignKey("ComponentProductId")]
    public virtual Product ComponentProduct { get; set; } = null!;

    [ForeignKey("ComponentVariantId")]
    public virtual ProductVariant? ComponentVariant { get; set; }

    [InverseProperty("Component")]
    public virtual ICollection<ProductBundleComponentSwap> Swaps { get; set; } = new List<ProductBundleComponentSwap>();
}
