using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductVariantId", "OptionName")]
[Table("ProductVariantOptionValue")]
public partial class ProductVariantOptionValue
{
    [Key]
    public int ProductVariantId { get; set; }

    [Key]
    [StringLength(100)]
    public string OptionName { get; set; } = null!;

    [StringLength(100)]
    public string OptionValue { get; set; } = null!;

    [ForeignKey("ProductVariantId")]
    [InverseProperty("ProductVariantOptionValues")]
    public virtual ProductVariant ProductVariant { get; set; } = null!;
}
