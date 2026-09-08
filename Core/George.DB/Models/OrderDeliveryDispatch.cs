using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One dispatch of an order to a delivery provider - audit/history row of the provider abstraction.
/// The latest state is denormalized onto Order.DeliveryProvider* for cheap list rendering.
/// </summary>
[Index(nameof(OrderId), nameof(ProviderKey))]
[Index(nameof(ProviderKey), nameof(ExternalTaskId))]
public partial class OrderDeliveryDispatch
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int OrderId { get; set; }

    public int SiteId { get; set; }

    [StringLength(30)]
    public string ProviderKey { get; set; } = null!;

    /// <summary>External task/delivery id at the provider (null when the create attempt failed).</summary>
    [StringLength(64)]
    public string? ExternalTaskId { get; set; }

    [StringLength(500)]
    public string? TrackingLink { get; set; }

    /// <summary>dispatched | failed | cancelled | courier statuses pushed by webhooks.</summary>
    [StringLength(30)]
    public string Status { get; set; } = null!;

    [StringLength(500)]
    public string? ErrorMessage { get; set; }

    public int AttemptCount { get; set; }

    [Precision(0)]
    public DateTime? DispatchedAt { get; set; }

    [Precision(0)]
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>Raw courier status pushed by the provider webhook (provider-specific value).</summary>
    [StringLength(50)]
    public string? CourierStatus { get; set; }

    [Precision(0)]
    public DateTime? CourierStatusUpdatedAt { get; set; }

    [ForeignKey(nameof(OrderId))]
    public virtual Order? Order { get; set; }
}
