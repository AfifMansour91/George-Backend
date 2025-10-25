using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Keyless]
public partial class VwAccountCategoryTree
{
    public long AccountCategoryId { get; set; }

    public long AccountId { get; set; }

    public long? ParentAccountCategoryId { get; set; }

    public int SortOrder { get; set; }

    public bool IsEnabled { get; set; }

    [StringLength(200)]
    public string? DisplayName { get; set; }

    [StringLength(200)]
    public string BaseName { get; set; } = null!;

    [StringLength(200)]
    public string? BaseSlug { get; set; }
}
