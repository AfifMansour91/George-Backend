using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateAttributeId", "Value")]
[Table("TemplateAttributeValue")]
public partial class TemplateAttributeValue
{
    [Key]
    public int TemplateAttributeId { get; set; }

    [Key]
    [StringLength(200)]
    public string Value { get; set; } = null!;

    [ForeignKey("TemplateAttributeId")]
    [InverseProperty("TemplateAttributeValues")]
    public virtual TemplateAttribute TemplateAttribute { get; set; } = null!;
}
