using System.ComponentModel.DataAnnotations;

namespace George.DB;

/// <summary>AI Product Studio feature gate on Account (AiStudio_Phase1_Install.sql). Pattern: <see cref="MarketingEnabled"/>.</summary>
public partial class Account
{
    public const string AiStudioModeCommunity = "community";
    public const string AiStudioModePaid = "paid";

    /// <summary>Super-admin switch for the AI Product Studio (image looks, "שפר ב-AI", /AiStudio API).</summary>
    public bool AiStudioEnabled { get; set; }

    /// <summary><c>community</c> (free, results donated to the global pool) | <c>paid</c> (private, credits). NULL = community.</summary>
    [StringLength(20)]
    public string? AiStudioMode { get; set; }

    /// <summary>Max AI jobs per calendar day for this account. NULL = platform default (AiStudio:DefaultDailyCap).</summary>
    public int? AiStudioDailyCap { get; set; }

    /// <summary>Community mode: when the shop accepted the community-pool terms (AiStudio_Phase3_Install.sql). NULL = not yet; no job runs before that.</summary>
    public DateTime? AiStudioPoolConsentAt { get; set; }
}
