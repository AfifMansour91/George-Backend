using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class BusinessType
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? Icon { get; set; }

    [InverseProperty("BusinessType")]
    public virtual ICollection<BusinessTypeCategory> BusinessTypeCategory { get; set; } = new List<BusinessTypeCategory>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("BusinessTypeCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("BusinessType")]
    public virtual ICollection<Media> Media { get; set; } = new List<Media>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("BusinessTypeUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("BusinessType")]
    public virtual ICollection<GlobalCategory> GlobalCategory { get; set; } = new List<GlobalCategory>();

    [ForeignKey("BusinessTypeId")]
    [InverseProperty("BusinessType")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();
}
