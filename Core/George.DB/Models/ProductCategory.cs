using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductId", "CategoryId")]
[Table("ProductCategory")]
[Index("CategoryId", Name = "IX_ProductCategory_CategoryId")]
public partial class ProductCategory
{
    [Key]
    public int ProductId { get; set; }

    [Key]
    public int CategoryId { get; set; }

    public bool IsPrimary { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("ProductCategories")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("ProductCategories")]
    public virtual Product Product { get; set; } = null!;
}
