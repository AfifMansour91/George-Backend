using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AttributeOption")]
public partial class AttributeOption
{
    [Key]
    public int Id { get; set; }

    public int AttributeId { get; set; }

    [StringLength(150)]
    public string Value { get; set; } = null!;

    public int SortOrder { get; set; }

    [InverseProperty("AttributeOption")]
    public virtual ICollection<AccountProductAttributeValue> AccountProductAttributeValues { get; set; } = new List<AccountProductAttributeValue>();

    [InverseProperty("AttributeOption")]
    public virtual ICollection<AccountProductVariantOption> AccountProductVariantOptions { get; set; } = new List<AccountProductVariantOption>();

    [ForeignKey("AttributeId")]
    [InverseProperty("AttributeOptions")]
    public virtual Attribute Attribute { get; set; } = null!;

    [InverseProperty("AttributeOption")]
    public virtual ICollection<ProductTemplateAttributeOption> ProductTemplateAttributeOptions { get; set; } = new List<ProductTemplateAttributeOption>();
}
