using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("SubParcel")]
[Index("RegistryUnitId", "SubParcel1", Name = "IX_SubParcel_RegistryUnitId_SubParcel_Unique", IsUnique = true)]
public partial class SubParcel
{
    [Key]
    public int Id { get; set; }

    public int RegistryUnitId { get; set; }

    [Column("SubParcel")]
    public int SubParcel1 { get; set; }

    [ForeignKey("RegistryUnitId")]
    [InverseProperty("SubParcels")]
    public virtual RegistryUnit RegistryUnit { get; set; } = null!;
}
