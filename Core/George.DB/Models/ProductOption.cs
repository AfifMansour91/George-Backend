using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductOption")]
public partial class ProductOption
{
    [Key]
    public int Id { get; set; }

    public int ProductId { get; set; }

    [StringLength(100)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [ForeignKey("ProductId")]
    [InverseProperty("ProductOptions")]
    public virtual Product Product { get; set; } = null!;

    [InverseProperty("ProductOption")]
    public virtual ICollection<ProductOptionValue> ProductOptionValues { get; set; } = new List<ProductOptionValue>();
}
