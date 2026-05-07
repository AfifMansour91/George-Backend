using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductId", "BrandId")]
[Index("BrandId", Name = "IX_ProductBrand_BrandId")]
public partial class ProductBrand
{
    [Key]
    public int ProductId { get; set; }

    [Key]
    public int BrandId { get; set; }

    public bool IsPrimary { get; set; }

    [ForeignKey("BrandId")]
    [InverseProperty("ProductBrand")]
    public virtual Brand Brand { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("ProductBrand")]
    public virtual Product Product { get; set; } = null!;
}
