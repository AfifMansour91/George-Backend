using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One row per SMS that leaves the system - operational (order confirmation, invoice...) and marketing alike.
/// Before this table a sent SMS left no trace, so nothing could be counted, billed or explained to a shop.
/// Written best-effort through <c>MessageLogQueue</c>; never blocks or fails the send itself.
/// </summary>
[Index("AccountId", "CreationTime", Name = "IX_MessageLog_AccountId_CreationTime")]
public partial class MessageLog
{
    [Key]
    public long Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    public int? AccountId { get; set; }

    public int? SiteId { get; set; }

    /// <summary><c>operational</c> | <c>marketing</c> | <c>system</c> (OTP / test - never billed to the shop).</summary>
    [StringLength(20)]
    public string Kind { get; set; } = null!;

    [StringLength(40)]
    public string Category { get; set; } = null!;

    [StringLength(20)]
    public string Channel { get; set; } = "sms";

    [StringLength(50)]
    public string NormalizedPhone { get; set; } = null!;

    /// <summary>Billable SMS segments (GSM-7 160/153, UCS-2 70/67).</summary>
    public int Units { get; set; }

    [StringLength(30)]
    public string? Provider { get; set; }

    /// <summary>True when the account's own SMS credentials were used (the shop pays its provider directly).</summary>
    public bool UsedAccountConfig { get; set; }

    public bool Success { get; set; }

    [StringLength(500)]
    public string? Error { get; set; }

    public int? OrderId { get; set; }

    public long? MarketingDeliveryId { get; set; }
}
