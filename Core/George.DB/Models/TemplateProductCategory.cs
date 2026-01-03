using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("TemplateProductId", "CategoryId")]
[Table("TemplateProductCategory")]
public partial class TemplateProductCategory
{
    [Key]
    public int TemplateProductId { get; set; }

    [Key]
    public int CategoryId { get; set; }

    public bool IsPrimary { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("TemplateProductCategories")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("TemplateProductId")]
    [InverseProperty("TemplateProductCategories")]
    public virtual TemplateProduct TemplateProduct { get; set; } = null!;
}
