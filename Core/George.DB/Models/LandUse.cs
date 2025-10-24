using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("LandUse")]
[Index("Name", Name = "IX_LandUse_Name_Unique", IsUnique = true)]
public partial class LandUse
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string Name { get; set; } = null!;

    [InverseProperty("LandUse")]
    public virtual ICollection<RegistryUnit> RegistryUnits { get; set; } = new List<RegistryUnit>();
}
