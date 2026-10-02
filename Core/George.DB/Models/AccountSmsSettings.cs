using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Per-account SMS provider credentials, one row per provider: the shop's own ActiveTrail and the Inforu sub-account
/// Giorgio opened for it can coexist. <see cref="IsEnabled"/> marks the ACTIVE row (at most one per account);
/// no enabled row = the account uses the system-wide SMS account.
/// </summary>
[Index("AccountId", "Provider", Name = "UQ_AccountSmsSettings_AccountId_Provider", IsUnique = true)]
public partial class AccountSmsSettings
{
    [Key]
    public int Id { get; set; }

    public int AccountId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    /// <summary>This row is the account's active SMS provider. False keeps the credentials for later; no enabled row = system default.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>SMS provider name: &quot;ActiveTrail&quot; or &quot;Inforu&quot;.</summary>
    [StringLength(20)]
    public string Provider { get; set; } = "ActiveTrail";

    /// <summary>Optional provider API URL override; NULL = provider default URL.</summary>
    [StringLength(500)]
    public string? ApiBaseUrl { get; set; }

    /// <summary>Inforu API username (Basic auth = username:token). Not used by ActiveTrail.</summary>
    [StringLength(100)]
    public string? Username { get; set; }

    [StringLength(500)]
    public string? ApiToken { get; set; }

    /// <summary>Sender/display name shown to the SMS recipient.</summary>
    [StringLength(100)]
    public string? FromName { get; set; }

    /// <summary>Reserved for providers that send from a phone number (ActiveTrail uses FromName).</summary>
    [StringLength(50)]
    public string? SourcePhone { get; set; }

    /// <summary>
    /// The credentials are a SUB-ACCOUNT the platform opened for this shop under its own provider account (Inforu
    /// parent → child). The platform pays the provider, so the shop is still metered by George's marketing bank -
    /// unlike a shop that brought its own provider account. Super-admin only.
    /// </summary>
    public bool BilledByPlatform { get; set; }

    /// <summary>The sub-account's numeric customer id in Inforu - the LevelValue of Admin/CreateOrAddQuota. Learned automatically from GetQuota; editable by super-admin.</summary>
    [StringLength(50)]
    public string? InforuCustomerId { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountSmsSettings")]
    public virtual Account Account { get; set; } = null!;
}
