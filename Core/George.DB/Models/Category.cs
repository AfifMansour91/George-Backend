using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Index("AccountId", Name = "IX_Category_AccountId")]
[Index("SourceGlobalCategoryId", Name = "IX_Category_SourceGlobalCategoryId")]
public partial class Category
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

    public bool IsActive { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    public int? ParentCategoryId { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(200)]
    public string? CustomName { get; set; }

    public bool? IsEnabled { get; set; }

    public int? SortOrder { get; set; }

    public bool? DisplayAsMain { get; set; }

    public int? AccountId { get; set; }

    public int? SourceGlobalCategoryId { get; set; }

    public int? WooCommerceId { get; set; }

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    public string? IconUrl { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Category")]
    public virtual Account? Account { get; set; }

    [ForeignKey("CreationUserId")]
    [InverseProperty("CategoryCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("ParentCategory")]
    public virtual ICollection<Category> InverseParentCategory { get; set; } = new List<Category>();

    [ForeignKey("ParentCategoryId")]
    [InverseProperty("InverseParentCategory")]
    public virtual Category? ParentCategory { get; set; }

    [InverseProperty("Category")]
    public virtual ICollection<ProductCategory> ProductCategory { get; set; } = new List<ProductCategory>();

    [ForeignKey("SourceGlobalCategoryId")]
    [InverseProperty("Category")]
    public virtual GlobalCategory? SourceGlobalCategory { get; set; }

    [ForeignKey("UpdateUserId")]
    [InverseProperty("CategoryUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("CategoryId")]
    [InverseProperty("Category")]
    public virtual ICollection<Media> Media { get; set; } = new List<Media>();

    [ForeignKey("CategoryId")]
    [InverseProperty("Category")]
    public virtual ICollection<Site> Site { get; set; } = new List<Site>();
}
