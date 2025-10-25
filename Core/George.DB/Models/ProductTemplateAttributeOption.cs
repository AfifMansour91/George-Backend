using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductTemplateAttributeOption")]
public partial class ProductTemplateAttributeOption
{
    [Key]
    public long Id { get; set; }

    public long ProductTemplateAttributeId { get; set; }

    public int AttributeOptionId { get; set; }

    [ForeignKey("AttributeOptionId")]
    [InverseProperty("ProductTemplateAttributeOptions")]
    public virtual AttributeOption AttributeOption { get; set; } = null!;

    [ForeignKey("ProductTemplateAttributeId")]
    [InverseProperty("ProductTemplateAttributeOptions")]
    public virtual ProductTemplateAttribute ProductTemplateAttribute { get; set; } = null!;
}
