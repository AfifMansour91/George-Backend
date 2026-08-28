using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Per-account SMS provider credentials. No row (or IsEnabled=false / empty token) = account uses the system-wide SMS account.</summary>
[Index("AccountId", Name = "UQ_AccountSmsSettings_AccountId", IsUnique = true)]
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

    /// <summary>Master switch: false keeps the saved credentials but sends through the system default account.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>SMS provider name. Currently only &quot;ActiveTrail&quot; is supported; column exists so more providers can be added without a schema change.</summary>
    [StringLength(20)]
    public string Provider { get; set; } = "ActiveTrail";

    /// <summary>Optional provider API URL override; NULL = system default URL.</summary>
    [StringLength(500)]
    public string? ApiBaseUrl { get; set; }

    [StringLength(500)]
    public string? ApiToken { get; set; }

    /// <summary>Sender/display name shown to the SMS recipient.</summary>
    [StringLength(100)]
    public string? FromName { get; set; }

    /// <summary>Reserved for providers that send from a phone number (ActiveTrail uses FromName).</summary>
    [StringLength(50)]
    public string? SourcePhone { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("AccountSmsSettings")]
    public virtual Account Account { get; set; } = null!;
}
