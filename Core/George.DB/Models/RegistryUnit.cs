using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("RegistryUnit")]
[Index("Block", "Parcel", Name = "IX_RegistryUnit_Block_Parcel_Unique", IsUnique = true)]
public partial class RegistryUnit
{
    [Key]
    public int Id { get; set; }

    public int OwnershipTypeId { get; set; }

    public int? RegistrationMethodId { get; set; }

    public int? LandUseId { get; set; }

    public int Block { get; set; }

    public int Parcel { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [Column(TypeName = "decimal(10, 2)")]
    public decimal? Area { get; set; }

    public string? Data { get; set; }

    public int IsFilled { get; set; }

    [Precision(0)]
    public DateTime? UpdateTime { get; set; }

    [ForeignKey("LandUseId")]
    [InverseProperty("RegistryUnits")]
    public virtual LandUse? LandUse { get; set; }

    [InverseProperty("RegistryUnit")]
    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();

    [InverseProperty("RegistryUnit")]
    public virtual ICollection<SubParcel> SubParcels { get; set; } = new List<SubParcel>();
}
