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

    [StringLength(150)]
    public string Name { get; set; } = null!;

    [StringLength(30)]
    public string DataType { get; set; } = null!;

    public bool IsVariantAxis { get; set; }

    [InverseProperty("Attribute")]
    public virtual ICollection<AccountProductAttribute> AccountProductAttributes { get; set; } = new List<AccountProductAttribute>();

    [InverseProperty("Attribute")]
    public virtual ICollection<AccountProductVariantOption> AccountProductVariantOptions { get; set; } = new List<AccountProductVariantOption>();

    [InverseProperty("Attribute")]
    public virtual ICollection<AttributeOption> AttributeOptions { get; set; } = new List<AttributeOption>();

    [InverseProperty("Attribute")]
    public virtual ICollection<ProductTemplateAttribute> ProductTemplateAttributes { get; set; } = new List<ProductTemplateAttribute>();
}
