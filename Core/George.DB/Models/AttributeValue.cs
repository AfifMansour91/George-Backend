using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("AttributeId", "Value")]
public partial class AttributeValue
{
    [Key]
    public int AttributeId { get; set; }

    [Key]
    [StringLength(200)]
    public string Value { get; set; } = null!;

    /// <summary>Manual value order inside the attribute (lower = first). NULL = never ordered: sorts after ordered values, alphabetically.</summary>
    public int? DisplayOrder { get; set; }

    [ForeignKey("AttributeId")]
    [InverseProperty("AttributeValue")]
    public virtual Attribute Attribute { get; set; } = null!;
}
