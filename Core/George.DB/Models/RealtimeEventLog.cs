using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Server → client SignalR push diagnostics (all hubs / features).</summary>
[Index(nameof(HubName), nameof(Feature), nameof(CreationTime))]
[Index(nameof(SiteId), nameof(CreationTime))]
[Index(nameof(EntityType), nameof(EntityId), nameof(CreationTime))]
[Index(nameof(CreationTime))]
public partial class RealtimeEventLog
{
    [Key]
    public long Id { get; set; }

    [StringLength(64)]
    public string HubName { get; set; } = null!;

    [StringLength(64)]
    public string Feature { get; set; } = null!;

    /// <summary>Client method name (e.g. NewOrderCreated).</summary>
    [StringLength(64)]
    public string EventName { get; set; } = null!;

    public int? SiteId { get; set; }

    public int? AccountId { get; set; }

    [StringLength(32)]
    public string? EntityType { get; set; }

    [StringLength(64)]
    public string? EntityId { get; set; }

    public string? PayloadJson { get; set; }

    public bool Success { get; set; } = true;

    [StringLength(500)]
    public string? Detail { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }
}
