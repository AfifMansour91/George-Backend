using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("EcomCategoryMap")]
public partial class EcomCategoryMap
{
    [Key]
    public long Id { get; set; }

    public long AccountCategoryId { get; set; }

    [StringLength(100)]
    public string RemoteCategoryId { get; set; } = null!;

    public DateTime SyncedAt { get; set; }

    [ForeignKey("AccountCategoryId")]
    [InverseProperty("EcomCategoryMaps")]
    public virtual AccountCategory AccountCategory { get; set; } = null!;
}
