using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Per-site configuration of one delivery provider (courier company) - the provider abstraction's
/// settings row. One row per (SiteId, ProviderKey); provider-specific extras go in SettingsJson.
/// </summary>
[Index(nameof(SiteId), nameof(ProviderKey), IsUnique = true)]
public partial class DeliveryProviderConfig
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int SiteId { get; set; }

    /// <summary>Stable provider key, e.g. "lionwheel".</summary>
    [StringLength(30)]
    public string ProviderKey { get; set; } = null!;

    public bool Enabled { get; set; }

    /// <summary>Provider API key/token.</summary>
    [StringLength(500)]
    public string? ApiKey { get; set; }

    /// <summary>Order status that triggers dispatch: "New" | "InTreatment" | "Ready".</summary>
    [StringLength(30)]
    public string? TriggerStatus { get; set; }

    /// <summary>Package pickup (source) address sent to the provider.</summary>
    [StringLength(120)]
    public string? PickupCity { get; set; }

    [StringLength(200)]
    public string? PickupStreet { get; set; }

    [StringLength(30)]
    public string? PickupNumber { get; set; }

    [StringLength(120)]
    public string? PickupName { get; set; }

    [StringLength(50)]
    public string? PickupPhone { get; set; }

    /// <summary>Secret required on the provider's incoming status webhook URL for this site.</summary>
    [StringLength(64)]
    public string? WebhookSecret { get; set; }

    /// <summary>Provider-specific extra settings (JSON), so new providers don't need new columns.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? SettingsJson { get; set; }

    [ForeignKey(nameof(SiteId))]
    public virtual Site? Site { get; set; }
}
