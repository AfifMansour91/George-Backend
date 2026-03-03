using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("MediaId", Name = "IX_ProductImage_MediaId")]
[Index("ProductId", "Url", Name = "IX_ProductImage_ProductId_Url", IsUnique = true)]
public partial class ProductImage
{
    public int ProductId { get; set; }

    public int SortOrder { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    public int? MediaId { get; set; }

    [Key]
    public long Id { get; set; }

    [ForeignKey("MediaId")]
    [InverseProperty("ProductImage")]
    public virtual Media? Media { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductImage")]
    public virtual Product Product { get; set; } = null!;
}
