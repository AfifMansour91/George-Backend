using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Keyless]
public partial class VwAccountProductList
{
    public long AccountProductId { get; set; }

    public long AccountId { get; set; }

    [StringLength(200)]
    public string AccountName { get; set; } = null!;

    [StringLength(300)]
    public string? DisplayTitle { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    public bool IsEnabled { get; set; }

    [StringLength(30)]
    public string EditingStatus { get; set; } = null!;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? BaseUnitPrice { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal? StockQuantity { get; set; }

    [StringLength(20)]
    public string? BaseUnitCode { get; set; }

    public bool? IsKosher { get; set; }

    [StringLength(50)]
    public string? KosherStatusName { get; set; }

    [StringLength(40)]
    public string? WeightModelCode { get; set; }

    [StringLength(100)]
    public string? WeightModelName { get; set; }

    public string? CategoryNames { get; set; }

    [StringLength(1000)]
    public string? PrimaryImageUrl { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
