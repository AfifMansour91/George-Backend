using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductImage")]
public partial class ProductImage
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public int ProductId { get; set; }

    public int SortOrder { get; set; }

    [StringLength(1000)]
    public string Url { get; set; } = null!;

    public int? MediaId { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductImages")]
    public virtual Product Product { get; set; } = null!;

    [ForeignKey("MediaId")]
    [InverseProperty("ProductImages")]
    public virtual Medium? Media { get; set; }
}
