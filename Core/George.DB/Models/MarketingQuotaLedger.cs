using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Marketing message quota as a journal of movements, not a counter - so a shop can always be shown where
/// its messages went. Balance of a bucket = SUM(Amount).
/// </summary>
[Index("AccountId", "Bucket", Name = "IX_MarketingQuotaLedger_AccountId_Bucket")]
public partial class MarketingQuotaLedger
{
    [Key]
    public long Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    public int AccountId { get; set; }

    /// <summary><c>allocation</c> | <c>purchase</c> | <c>consumption</c> | <c>refund</c>.</summary>
    [StringLength(20)]
    public string EntryType { get; set; } = null!;

    /// <summary><c>bank</c> (never expires) | <c>monthly</c>.</summary>
    [StringLength(20)]
    public string Bucket { get; set; } = "bank";

    /// <summary>Positive = in, negative = out.</summary>
    public int Amount { get; set; }

    [StringLength(7)]
    public string? Period { get; set; }

    public int? RefSendId { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    public int? CreationUserId { get; set; }
}
