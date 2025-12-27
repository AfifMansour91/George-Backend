using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountCategorySite")]
public partial class AccountCategorySite
{
    [Key]
    public long Id { get; set; }

    public long AccountCategoryId { get; set; }

    public long SiteId { get; set; }

    public bool IsEnabled { get; set; }

    public int? SortOrder { get; set; }

    [ForeignKey("AccountCategoryId")]
    [InverseProperty("AccountCategorySites")]
    public virtual AccountCategory AccountCategory { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("AccountCategorySites")]
    public virtual Site Site { get; set; } = null!;
}
