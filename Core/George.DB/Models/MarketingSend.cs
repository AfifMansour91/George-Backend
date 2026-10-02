using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One marketing send (campaign). Creating it only stores the definition; the dispatcher resolves the
/// audience at <see cref="ScheduledAt"/> ("send now" = creation time) and writes one
/// <see cref="MarketingDelivery"/> per audience member - those rows are the audience snapshot.
/// </summary>
[Index("AccountId", "ScheduledAt", Name = "IX_MarketingSend_AccountId_ScheduledAt")]
[Index("Status", "ScheduledAt", Name = "IX_MarketingSend_Status_ScheduledAt")]
public partial class MarketingSend
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime UpdateTime { get; set; }

    [StringLength(20)]
    public string Type { get; set; } = "manual";

    [StringLength(200)]
    public string Name { get; set; } = null!;

    [StringLength(20)]
    public string Channel { get; set; } = "sms";

    /// <summary><c>scheduled</c> | <c>sending</c> | <c>sent</c> | <c>canceled</c> | <c>failed</c>.</summary>
    [StringLength(20)]
    public string Status { get; set; } = null!;

    /// <summary>Why a <c>sending</c> send is not progressing: <c>no_quota</c> | <c>send_window</c>; null when flowing.</summary>
    [StringLength(20)]
    public string? PausedReason { get; set; }

    /// <summary><c>all</c> | <c>segment</c> | <c>filter</c>.</summary>
    [StringLength(20)]
    public string AudienceType { get; set; } = null!;

    public int? SegmentId { get; set; }

    [StringLength(40)]
    public string? SystemSegmentKey { get; set; }

    [StringLength(300)]
    public string? AudienceLabel { get; set; }

    public string? AudienceDefinitionJson { get; set; }

    /// <summary>Branch scope as a JSON int array.</summary>
    public string SiteIdsJson { get; set; } = "[]";

    [StringLength(2000)]
    public string Body { get; set; } = null!;

    [StringLength(1000)]
    public string? LinkUrl { get; set; }

    [Precision(0)]
    public DateTime ScheduledAt { get; set; }

    /// <summary>The time the shop asked for, kept when the send was deferred (Shabbat / holiday / quiet hours).</summary>
    [Precision(0)]
    public DateTime? OriginalScheduledAt { get; set; }

    [Precision(0)]
    public DateTime? StartedAt { get; set; }

    [Precision(0)]
    public DateTime? CompletedAt { get; set; }

    /// <summary>The attribution window in force when the send was created - later setting changes never rewrite history.</summary>
    public int AttributionWindowHours { get; set; } = 72;

    public int AudienceCount { get; set; }

    public int PlannedCount { get; set; }

    public int? CreationUserId { get; set; }
}
