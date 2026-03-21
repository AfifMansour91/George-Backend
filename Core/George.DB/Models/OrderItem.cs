using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("OrderId", Name = "IX_OrderItem_OrderId")]
public partial class OrderItem
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [StringLength(500)]
    public string? Title { get; set; }

    [StringLength(200)]
    public string? VariantTitle { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? UnitWeightGrams { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? PricePerUnit { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TotalPrice { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>WooCommerce line: <c>saleUnits</c> as sent (e.g. "1 יח'").</summary>
    [StringLength(100)]
    public string? SaleUnits { get; set; }

    /// <summary>WooCommerce line: <c>saleTotalWeight</c> as sent (e.g. "0.8 ").</summary>
    [StringLength(100)]
    public string? SaleTotalWeight { get; set; }

    /// <summary>WooCommerce product id for this line (for support / sync).</summary>
    public int? WooCommerceProductId { get; set; }

    /// <summary>WooCommerce variation id when applicable.</summary>
    public int? WooCommerceVariationId { get; set; }

    public int SortOrder { get; set; }

    /// <summary>
    /// How UI should render quantity: <c>total_weight</c> (line weight), <c>per_unit</c> (units + per-unit grams), <c>units_only</c>.
    /// Set from client (manual/kiosk) or computed for WooCommerce ingest. Matches shop-manager <c>weightDisplayMode</c> JSON.
    /// </summary>
    [StringLength(32)]
    public string? WeightDisplayMode { get; set; }

    /// <summary>Shop UI: <c>units</c> (יח׳) vs <c>weight</c> (משקל). CamelCase JSON from API.</summary>
    [StringLength(16)]
    public string? OrderLineQuantityMode { get; set; }

    /// <summary>Preformatted Hebrew label e.g. <c>600 גרם ליח'</c>.</summary>
    [StringLength(120)]
    public string? OrderLinePerUnitWeightLabel { get; set; }

    /// <summary>Size + approximate weight e.g. <c>גדול (כ 0.8 ק"ג)</c>.</summary>
    [StringLength(200)]
    public string? OrderLineSizeLabel { get; set; }

    /// <summary>Cutting option value only.</summary>
    [StringLength(200)]
    public string? OrderLineCuttingLabel { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? PickedQuantity { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderItem")]
    public virtual Order Order { get; set; } = null!;
}
