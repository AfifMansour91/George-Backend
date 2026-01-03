using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductId", "Url")]
[Table("ProductImage")]
public partial class ProductImage
{
    [Key]
    public int ProductId { get; set; }

    public int SortOrder { get; set; }

    [Key]
    [StringLength(1000)]
    public string Url { get; set; } = null!;

    [ForeignKey("ProductId")]
    [InverseProperty("ProductImages")]
    public virtual Product Product { get; set; } = null!;
}
