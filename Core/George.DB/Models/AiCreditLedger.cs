using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// AI Studio credits as a journal of movements, not a counter (pattern: <see cref="MarketingQuotaLedger"/>), so a shop can always
/// be shown where its credits went. Balance = SUM(Amount). AiStudio_Phase3_Install.sql.
/// </summary>
[Index("AccountId", Name = "IX_AiCreditLedger_AccountId")]
public partial class AiCreditLedger
{
    public const string TypeAllocation = "allocation";
    public const string TypePurchase = "purchase";
    public const string TypeConsumption = "consumption";
    public const string TypeRefund = "refund";

    [Key]
    public long Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    public int AccountId { get; set; }

    /// <summary><c>allocation</c> (super-admin) | <c>purchase</c> | <c>consumption</c> (a job) | <c>refund</c> (a failed job).</summary>
    [StringLength(20)]
    public string EntryType { get; set; } = null!;

    /// <summary>Positive = in, negative = out.</summary>
    public int Amount { get; set; }

    /// <summary>The <see cref="AiImageJob"/> for consumption / refund.</summary>
    public long? RefJobId { get; set; }

    /// <summary>The <see cref="AiCreditPurchase"/> for a purchase.</summary>
    public int? RefPurchaseId { get; set; }

    [StringLength(300)]
    public string? Note { get; set; }

    public int? CreationUserId { get; set; }
}
