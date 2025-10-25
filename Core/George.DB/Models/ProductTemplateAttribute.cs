using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("ProductTemplateAttribute")]
public partial class ProductTemplateAttribute
{
    [Key]
    public long Id { get; set; }

    public long ProductTemplateId { get; set; }

    public int AttributeId { get; set; }

    public bool IsVariantAxis { get; set; }

    [ForeignKey("AttributeId")]
    [InverseProperty("ProductTemplateAttributes")]
    public virtual Attribute Attribute { get; set; } = null!;

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplateAttributes")]
    public virtual ProductTemplate ProductTemplate { get; set; } = null!;

    [InverseProperty("ProductTemplateAttribute")]
    public virtual ICollection<ProductTemplateAttributeOption> ProductTemplateAttributeOptions { get; set; } = new List<ProductTemplateAttributeOption>();
}
