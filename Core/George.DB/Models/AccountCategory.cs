using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountCategory")]
public partial class AccountCategory
{
    [Key]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public int CategoryId { get; set; }

    public long? ParentAccountCategoryId { get; set; }

    [StringLength(200)]
    public string? CustomName { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountCategories")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("AccountCategory")]
    public virtual ICollection<AccountProductCategory> AccountProductCategories { get; set; } = new List<AccountProductCategory>();

    [ForeignKey("CategoryId")]
    [InverseProperty("AccountCategories")]
    public virtual Category Category { get; set; } = null!;

    [InverseProperty("AccountCategory")]
    public virtual ICollection<EcomCategoryMap> EcomCategoryMaps { get; set; } = new List<EcomCategoryMap>();

    [InverseProperty("ParentAccountCategory")]
    public virtual ICollection<AccountCategory> InverseParentAccountCategory { get; set; } = new List<AccountCategory>();

    [ForeignKey("ParentAccountCategoryId")]
    [InverseProperty("InverseParentAccountCategory")]
    public virtual AccountCategory? ParentAccountCategory { get; set; }
}
