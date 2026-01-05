using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductId", "GlobalCategoryId")]
[Table("TemplateProductCategory")]
public partial class TemplateProductCategory
{
    [Key]
    public int TemplateProductId { get; set; }

    public bool IsPrimary { get; set; }

    [Key]
    public int GlobalCategoryId { get; set; }

    [ForeignKey("GlobalCategoryId")]
    [InverseProperty("TemplateProductCategories")]
    public virtual GlobalCategory GlobalCategory { get; set; } = null!;

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductCategories")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;
}
