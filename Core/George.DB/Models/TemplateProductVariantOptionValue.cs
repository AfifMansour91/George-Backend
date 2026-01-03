using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductVariantId", "OptionName")]
[Table("TemplateProductVariantOptionValue")]
public partial class TemplateProductVariantOptionValue
{
    [Key]
    public int TemplateProductVariantId { get; set; }

    [Key]
    [StringLength(100)]
    public string OptionName { get; set; } = null!;

    [StringLength(100)]
    public string OptionValue { get; set; } = null!;

    [ForeignKey("TemplateProductVariantId")]
    [InverseProperty("TemplateProductVariantOptionValues")]
    public virtual TemplateProductVariant TemplateProductVariant { get; set; } = null!;
}
