using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Attribute")]
public partial class Attribute
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

    public int SiteId { get; set; }

    public int? WooCommerceId { get; set; }

    [InverseProperty("Attribute")]
    public virtual ICollection<AttributeValue> AttributeValues { get; set; } = new List<AttributeValue>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("AttributeCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [ForeignKey("SiteId")]
    [InverseProperty("Attributes")]
    public virtual Site Site { get; set; } = null!;

    [ForeignKey("UpdateUserId")]
    [InverseProperty("AttributeUpdateUsers")]
    public virtual User? UpdateUser { get; set; }
}
