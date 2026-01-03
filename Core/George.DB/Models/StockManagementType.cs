using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("StockManagementType")]
[Index("Name", Name = "UQ__StockMan__737584F62CB3B34A", IsUnique = true)]
public partial class StockManagementType
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("StockManagementType")]
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();

    [InverseProperty("StockManagementType")]
    public virtual ICollection<TemplateProduct> TemplateProducts { get; set; } = new List<TemplateProduct>();
}
