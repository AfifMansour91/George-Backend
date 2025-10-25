using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductAttributeValue")]
public partial class AccountProductAttributeValue
{
    [Key]
    public long Id { get; set; }

    public long AccountProductAttributeId { get; set; }

    public int? AttributeOptionId { get; set; }

    [StringLength(500)]
    public string? ValueText { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("AccountProductAttributeId")]
    [InverseProperty("AccountProductAttributeValues")]
    public virtual AccountProductAttribute AccountProductAttribute { get; set; } = null!;

    [ForeignKey("AttributeOptionId")]
    [InverseProperty("AccountProductAttributeValues")]
    public virtual AttributeOption? AttributeOption { get; set; }
}
