using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>SignalR hub connection / group join diagnostics (all hubs).</summary>
[Index(nameof(HubName), nameof(CreationTime))]
[Index(nameof(UserId), nameof(CreationTime))]
[Index(nameof(SiteId), nameof(CreationTime))]
public partial class RealtimeHubLog
{
    [Key]
    public long Id { get; set; }

    [StringLength(64)]
    public string HubName { get; set; } = null!;

    /// <summary>Feature area within the hub (e.g. NewOrder). Null for generic connection events.</summary>
    [StringLength(64)]
    public string? Feature { get; set; }

    [StringLength(32)]
    public string EventType { get; set; } = null!;

    [StringLength(128)]
    public string ConnectionId { get; set; } = null!;

    public int? UserId { get; set; }

    public int? SiteId { get; set; }

    public int? AccountId { get; set; }

    [StringLength(500)]
    public string? Detail { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }
}
