using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductAttribute")]
public partial class AccountProductAttribute
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    public int AttributeId { get; set; }

    public bool IsVariantAxis { get; set; }

    [ForeignKey("AccountProductId")]
    [InverseProperty("AccountProductAttributes")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;

    [InverseProperty("AccountProductAttribute")]
    public virtual ICollection<AccountProductAttributeValue> AccountProductAttributeValues { get; set; } = new List<AccountProductAttributeValue>();

    [ForeignKey("AttributeId")]
    [InverseProperty("AccountProductAttributes")]
    public virtual Attribute Attribute { get; set; } = null!;
}
