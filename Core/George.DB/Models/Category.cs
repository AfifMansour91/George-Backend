using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("Category")]
public partial class Category
{
    [Key]
    public int Id { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(200)]
    public string? Slug { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public int ProductCount { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? CreatedByUserId { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<AccountCategory> AccountCategories { get; set; } = new List<AccountCategory>();

    [InverseProperty("Category")]
    public virtual ICollection<CategoryBusinessType> CategoryBusinessTypes { get; set; } = new List<CategoryBusinessType>();

    [InverseProperty("ChildCategory")]
    public virtual ICollection<CategoryHierarchy> CategoryHierarchyChildCategories { get; set; } = new List<CategoryHierarchy>();

    [InverseProperty("ParentCategory")]
    public virtual ICollection<CategoryHierarchy> CategoryHierarchyParentCategories { get; set; } = new List<CategoryHierarchy>();

    [InverseProperty("Category")]
    public virtual ICollection<ProductTemplateCategory> ProductTemplateCategories { get; set; } = new List<ProductTemplateCategory>();

    [ForeignKey("CategoryId")]
    [InverseProperty("Categories")]
    public virtual ICollection<Medium> Media { get; set; } = new List<Medium>();
}
