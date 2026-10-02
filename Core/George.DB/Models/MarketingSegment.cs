using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// A SAVED segment. A segment is a definition, not a list - it is re-evaluated on every read.
/// The ready-made (system) segments are not stored; they live in code (<c>MarketingSystemSegments</c>).
/// </summary>
[Index("AccountId", Name = "IX_MarketingSegment_AccountId")]
public partial class MarketingSegment
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime UpdateTime { get; set; }

    [StringLength(200)]
    public string Name { get; set; } = null!;

    /// <summary>JSON array of <c>{axis, operator, value, unit}</c>; conditions are AND-ed, no nesting.</summary>
    public string DefinitionJson { get; set; } = null!;

    /// <summary>Pinned segments show as tabs on the customers screen (max 5 per account).</summary>
    public bool IsPinned { get; set; }

    public bool IsDeleted { get; set; }

    public int? CreationUserId { get; set; }
}
