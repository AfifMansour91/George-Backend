using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class TemplateProductVariant
{
    [Key]
    public int Id { get; set; }

    public int TemplateProductId { get; set; }

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

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductVariant")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;

    [InverseProperty("TemplateProductVariant")]
    public virtual ICollection<TemplateProductVariantOptionValue> TemplateProductVariantOptionValue { get; set; } = new List<TemplateProductVariantOptionValue>();
}
