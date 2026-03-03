using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductOptionId", "Value")]
public partial class TemplateProductOptionValue
{
    [Key]
    public int TemplateProductOptionId { get; set; }

    [Key]
    [StringLength(100)]
    public string Value { get; set; } = null!;

    [ForeignKey("TemplateProductOptionId")]
    [InverseProperty("TemplateProductOptionValue")]
    public virtual TemplateProductOption TemplateProductOption { get; set; } = null!;
}
