using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("StockStatus")]
[Index("Name", Name = "UQ__StockSta__737584F6B32B8F72", IsUnique = true)]
public partial class StockStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("StockStatus")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("StockStatus")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();
}
