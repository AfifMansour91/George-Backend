using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

[Keyless]
public partial class VwAccountProductStatusSummary
{
    public long AccountId { get; set; }

    [StringLength(200)]
    public string AccountName { get; set; } = null!;

    public int? TotalProducts { get; set; }

    public int? EnabledProducts { get; set; }

    public int? DisabledProducts { get; set; }

    public int? NotEditedCount { get; set; }

    public int? EditedCount { get; set; }

    public int? PublishedCount { get; set; }

    public DateTime? FirstProductCreatedAt { get; set; }

    public DateTime? LastProductUpdatedAt { get; set; }
}
