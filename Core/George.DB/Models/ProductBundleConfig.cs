using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Bundle (מארז) definition of a Product whose SetupType is 'bundle': pricing mode, discount and
/// storefront display options. One row per bundle product (PK = ProductId). Components live in
/// <see cref="ProductBundleComponent"/>. Spec: BUNDLES_SYNC_SPEC.md §2.
/// </summary>
public partial class ProductBundleConfig
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int ProductId { get; set; }

    /// <summary>fixed | sum</summary>
    [StringLength(10)]
    public string PricingMode { get; set; } = "fixed";

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? FixedPrice { get; set; }

    /// <summary>none | percent | fixed</summary>
    [StringLength(10)]
    public string DiscountType { get; set; } = "none";

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountValue { get; set; }

    /// <summary>unavailable | swap</summary>
    [StringLength(20)]
    public string OosBehavior { get; set; } = "unavailable";

    /// <summary>grid | list</summary>
    [StringLength(10)]
    public string Layout { get; set; } = "grid";

    /// <summary>name_with_components | name_only | line</summary>
    [StringLength(40)]
    public string CartDisplay { get; set; } = "name_with_components";

    /// <summary>bundle | components</summary>
    [StringLength(20)]
    public string InvoiceDisplay { get; set; } = "bundle";

    public bool HidePriceLabels { get; set; }

    /// <summary>sum mode only: parent line total follows the picked (weighed) component quantities.</summary>
    public bool ReweighPrice { get; set; }

    public bool ShowComponentsInDesc { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("BundleConfig")]
    public virtual Product Product { get; set; } = null!;
}
