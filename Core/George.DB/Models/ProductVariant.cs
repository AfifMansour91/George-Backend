using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class ProductVariant
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Price { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SalePrice { get; set; }

    public int? StockQuantity { get; set; }

    [StringLength(100)]
    public string? Sku { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Weight { get; set; }

    public bool IsDeleted { get; set; }

    public int? WooCommerceVariationId { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductVariant")]
    public virtual Product Product { get; set; } = null!;

    [InverseProperty("ProductVariant")]
    public virtual ICollection<ProductVariantOptionValue> ProductVariantOptionValue { get; set; } = new List<ProductVariantOptionValue>();
}
