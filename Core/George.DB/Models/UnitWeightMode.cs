using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("Name", Name = "UQ__UnitWeig__737584F63AE04B1E", IsUnique = true)]
public partial class UnitWeightMode
{
    [Key]
    public int Id { get; set; }

    [StringLength(30)]
    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    [InverseProperty("UnitWeightMode")]
    public virtual ICollection<WeightConfig> WeightConfig { get; set; } = new List<WeightConfig>();
}
