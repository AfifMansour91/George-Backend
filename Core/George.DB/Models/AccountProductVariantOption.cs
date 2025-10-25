using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductVariantOption")]
public partial class AccountProductVariantOption
{
    [Key]
    public long Id { get; set; }

    public long AccountProductVariantId { get; set; }

    public int AttributeId { get; set; }

    public int AttributeOptionId { get; set; }

    [ForeignKey("AccountProductVariantId")]
    [InverseProperty("AccountProductVariantOptions")]
    public virtual AccountProductVariant AccountProductVariant { get; set; } = null!;

    [ForeignKey("AttributeId")]
    [InverseProperty("AccountProductVariantOptions")]
    public virtual Attribute Attribute { get; set; } = null!;

    [ForeignKey("AttributeOptionId")]
    [InverseProperty("AccountProductVariantOptions")]
    public virtual AttributeOption AttributeOption { get; set; } = null!;
}
