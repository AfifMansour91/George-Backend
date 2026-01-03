using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductOptionId", "Value")]
[Table("ProductOptionValue")]
public partial class ProductOptionValue
{
    [Key]
    public int ProductOptionId { get; set; }

    [Key]
    [StringLength(100)]
    public string Value { get; set; } = null!;

    [ForeignKey("ProductOptionId")]
    [InverseProperty("ProductOptionValues")]
    public virtual ProductOption ProductOption { get; set; } = null!;
}
