using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("TemplateAttribute")]
public partial class TemplateAttribute
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

    [ForeignKey("CreationUserId")]
    [InverseProperty("TemplateAttributeCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("TemplateAttribute")]
    public virtual ICollection<TemplateAttributeValue> TemplateAttributeValues { get; set; } = new List<TemplateAttributeValue>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("TemplateAttributeUpdateUsers")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("TemplateAttributeId")]
    [InverseProperty("TemplateAttributes")]
    public virtual ICollection<Site> Sites { get; set; } = new List<Site>();
}
