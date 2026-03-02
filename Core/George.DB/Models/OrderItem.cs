using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Sprint 2: Line item on an order. References product/variant; stores snapshot (title, price) and weight/quantity.</summary>
[Table("OrderItem")]
public partial class OrderItem
{
    [Key]
    public int Id { get; set; }

    public int OrderId { get; set; }

    /// <summary>FK to Product (account product).</summary>
    public int? ProductId { get; set; }

    public int? ProductVariantId { get; set; }

    [StringLength(500)]
    public string? Title { get; set; }

    /// <summary>Variant/size description (e.g. "250g unit").</summary>
    [StringLength(200)]
    public string? VariantTitle { get; set; }

    /// <summary>Ordered quantity (e.g. 2 units or 1.5 kg).</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    /// <summary>Weight in grams per unit (for weighted products).</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? UnitWeightGrams { get; set; }

    [Column(TypeName = "decimal(18,4)")]
    public decimal? PricePerUnit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? TotalPrice { get; set; }

    /// <summary>Picked quantity/weight (kg or units) set during picking; null when not yet picked.</summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? PickedQuantity { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderItems")]
    public virtual Order Order { get; set; } = null!;
}
