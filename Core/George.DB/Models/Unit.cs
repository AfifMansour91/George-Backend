using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("Name", Name = "UQ__Unit__737584F662FEB2B6", IsUnique = true)]
public partial class Unit
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("Unit")]
    public virtual ICollection<WeightConfig> WeightConfig { get; set; } = new List<WeightConfig>();
}
