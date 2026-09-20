using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// An allowed replacement for one bundle component slot, with the surcharge (per bundle) charged
/// when it is chosen. Spec: BUNDLES_SYNC_SPEC.md §2.
/// </summary>
[Index("ComponentId", "IsDeleted", Name = "IX_ProductBundleComponentSwap_Component_IsDeleted")]
public partial class ProductBundleComponentSwap
{
    [Key]
    public int Id { get; set; }

    public int ComponentId { get; set; }

    public int SwapProductId { get; set; }

    public int? SwapVariantId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Surcharge { get; set; }

    /// <summary>
    /// The alternative's own quantity per bundle, in ITS unit (kg for a by_weight product, units otherwise).
    /// Null = inherit the slot quantity (converted by weight when the units differ).
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Qty { get; set; }

    public int SortOrder { get; set; }

    public bool IsDeleted { get; set; }

    [ForeignKey("ComponentId")]
    [InverseProperty("Swaps")]
    public virtual ProductBundleComponent Component { get; set; } = null!;

    [ForeignKey("SwapProductId")]
    public virtual Product SwapProduct { get; set; } = null!;

    [ForeignKey("SwapVariantId")]
    public virtual ProductVariant? SwapVariant { get; set; }
}
