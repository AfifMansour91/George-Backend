using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Unit")]
[Index("Code", Name = "UQ_Unit_Code", IsUnique = true)]
public partial class Unit
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [StringLength(20)]
    public string Code { get; set; } = null!;

    [InverseProperty("BaseUnit")]
    public virtual ICollection<AccountProduct> AccountProducts { get; set; } = new List<AccountProduct>();

    [InverseProperty("BaseUnit")]
    public virtual ICollection<ProductTemplate> ProductTemplates { get; set; } = new List<ProductTemplate>();
}
