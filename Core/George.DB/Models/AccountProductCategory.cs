using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Table("AccountProductCategory")]
public partial class AccountProductCategory
{
    [Key]
    public long Id { get; set; }

    public long AccountProductId { get; set; }

    public long AccountCategoryId { get; set; }

    [ForeignKey("AccountCategoryId")]
    [InverseProperty("AccountProductCategories")]
    public virtual AccountCategory AccountCategory { get; set; } = null!;

    [ForeignKey("AccountProductId")]
    [InverseProperty("AccountProductCategories")]
    public virtual AccountProduct AccountProduct { get; set; } = null!;
}
