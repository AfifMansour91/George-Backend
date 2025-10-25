using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[PrimaryKey("ParentCategoryId", "ChildCategoryId")]
[Table("CategoryHierarchy")]
public partial class CategoryHierarchy
{
    [Key]
    public int ParentCategoryId { get; set; }

    [Key]
    public int ChildCategoryId { get; set; }

    public int SortOrder { get; set; }

    [ForeignKey("ChildCategoryId")]
    [InverseProperty("CategoryHierarchyChildCategories")]
    public virtual Category ChildCategory { get; set; } = null!;

    [ForeignKey("ParentCategoryId")]
    [InverseProperty("CategoryHierarchyParentCategories")]
    public virtual Category ParentCategory { get; set; } = null!;
}
