using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One audience member of one send. Skipped members are kept (with <see cref="SkipReason"/>) - nothing is
/// dropped silently. <see cref="ShortToken"/> serves both the tracked link (/s/{token}) and the
/// unsubscribe link (/x/{token}) of this recipient.
/// </summary>
[Index("SendId", "Status", Name = "IX_MarketingDelivery_SendId_Status")]
[Index("AccountId", "NormalizedPhone", "SentAt", Name = "IX_MarketingDelivery_AccountId_Phone_SentAt")]
public partial class MarketingDelivery
{
    [Key]
    public long Id { get; set; }

    public int SendId { get; set; }

    public int AccountId { get; set; }

    public int SiteId { get; set; }

    public int CustomerId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(50)]
    public string NormalizedPhone { get; set; } = null!;

    /// <summary><c>queued</c> | <c>sending</c> | <c>sent</c> | <c>delivered</c> | <c>failed</c> | <c>skipped</c>.</summary>
    [StringLength(20)]
    public string Status { get; set; } = null!;

    [StringLength(20)]
    public string? SkipReason { get; set; }

    public int CostUnits { get; set; }

    [StringLength(16)]
    public string? ShortToken { get; set; }

    public int ClickCount { get; set; }

    [Precision(0)]
    public DateTime? FirstClickedAt { get; set; }

    [Precision(0)]
    public DateTime? LastClickedAt { get; set; }

    [Precision(0)]
    public DateTime? SentAt { get; set; }

    [Precision(0)]
    public DateTime? DeliveredAt { get; set; }

    [Precision(0)]
    public DateTime? UnsubscribedAt { get; set; }

    [StringLength(500)]
    public string? Error { get; set; }
}
