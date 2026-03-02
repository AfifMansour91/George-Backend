using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductId", "GlobalCategoryId")]
public partial class TemplateProductCategory
{
    [Key]
    public int TemplateProductId { get; set; }

    public bool IsPrimary { get; set; }

    [Key]
    public int GlobalCategoryId { get; set; }

    [ForeignKey("GlobalCategoryId")]
    [InverseProperty("TemplateProductCategory")]
    public virtual GlobalCategory GlobalCategory { get; set; } = null!;

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductCategory")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;
}
