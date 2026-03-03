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

    [ForeignKey("AttributeId")]
    [InverseProperty("AttributeValue")]
    public virtual Attribute Attribute { get; set; } = null!;
}
