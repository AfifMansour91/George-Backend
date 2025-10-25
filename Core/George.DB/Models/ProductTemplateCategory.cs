using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ProductTemplateId", "CategoryId")]
[Table("ProductTemplateCategory")]
public partial class ProductTemplateCategory
{
    [Key]
    public long ProductTemplateId { get; set; }

    [Key]
    public int CategoryId { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("ProductTemplateCategories")]
    public virtual Category Category { get; set; } = null!;

    [ForeignKey("ProductTemplateId")]
    [InverseProperty("ProductTemplateCategories")]
    public virtual ProductTemplate ProductTemplate { get; set; } = null!;
}
