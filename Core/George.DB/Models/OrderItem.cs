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

    public int SortOrder { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? PickedQuantity { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("OrderItem")]
    public virtual Order Order { get; set; } = null!;
}
