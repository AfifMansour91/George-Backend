using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Brand")]
public partial class Brand
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; }

    [InverseProperty("Brand")]
    public virtual ICollection<AccountProduct> AccountProducts { get; set; } = new List<AccountProduct>();

    [InverseProperty("Brand")]
    public virtual ICollection<ProductTemplate> ProductTemplates { get; set; } = new List<ProductTemplate>();
}
