using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("Name", Name = "UQ__StockSta__737584F6772BF6F7", IsUnique = true)]
public partial class StockStatus
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("StockStatus")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [InverseProperty("StockStatus")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();
}
