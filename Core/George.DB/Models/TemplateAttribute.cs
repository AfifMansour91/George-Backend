using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

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
    [InverseProperty("TemplateAttributeCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("TemplateAttribute")]
    public virtual ICollection<TemplateAttributeValue> TemplateAttributeValue { get; set; } = new List<TemplateAttributeValue>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("TemplateAttributeUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("TemplateAttributeId")]
    [InverseProperty("TemplateAttribute")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();
}
