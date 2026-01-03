using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductId", "Url")]
[Table("TemplateProductImage")]
public partial class TemplateProductImage
{
    [Key]
    public int TemplateProductId { get; set; }

    public int SortOrder { get; set; }

    [Key]
    [StringLength(1000)]
    public string Url { get; set; } = null!;

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductImages")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;
}
