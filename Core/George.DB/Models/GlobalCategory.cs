using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("GlobalCategory")]
public partial class GlobalCategory
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(2000)]
    public string? Description { get; set; }

    public int? ParentGlobalCategoryId { get; set; }

    public int? SortOrder { get; set; }

    public int? ProductCount { get; set; }

    [InverseProperty("SourceGlobalCategory")]
    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("GlobalCategoryCreationUsers")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("ParentGlobalCategory")]
    public virtual ICollection<GlobalCategory> InverseParentGlobalCategory { get; set; } = new List<GlobalCategory>();

    [ForeignKey("ParentGlobalCategoryId")]
    [InverseProperty("InverseParentGlobalCategory")]
    public virtual GlobalCategory? ParentGlobalCategory { get; set; }

    [InverseProperty("GlobalCategory")]
    public virtual ICollection<TemplateProductCategory> TemplateProductCategories { get; set; } = new List<TemplateProductCategory>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("GlobalCategoryUpdateUsers")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("GlobalCategoryId")]
    [InverseProperty("GlobalCategories")]
    public virtual ICollection<BusinessType> BusinessTypes { get; set; } = new List<BusinessType>();
}
