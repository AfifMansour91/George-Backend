using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One paid provider call (OpenAI / Gemini image, Anthropic vision) in USD - the platform's own cost journal per provider.
/// No provider exposes a balance API, so the wallet panel on the super-admin page is "the balance the admin typed in"
/// (<see cref="AiProviderTopUp"/>) minus the sum of these rows since then. AiStudio_Phase3_Install.sql §6.
/// </summary>
[Index("Provider", "CreationTime", Name = "IX_AiProviderSpend_Provider_CreationTime")]
public partial class AiProviderSpend
{
    public const string ProviderOpenAi = "openai";
    public const string ProviderGemini = "gemini";
    public const string ProviderAnthropic = "anthropic";

    public const string KindIdentify = "identify";
    public const string KindBrief = "brief";
    public const string KindJudge = "judge";
    public const string KindPreview = "preview";
    public const string KindFinal = "final";
    public const string KindDescribe = "describe";

    [Key]
    public long Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    /// <summary><c>openai</c> | <c>gemini</c> | <c>anthropic</c>.</summary>
    [StringLength(20)]
    public string Provider { get; set; } = null!;

    [StringLength(100)]
    public string? Model { get; set; }

    /// <summary><c>identify</c> | <c>brief</c> | <c>judge</c> | <c>preview</c> | <c>final</c> | <c>describe</c>.</summary>
    [StringLength(20)]
    public string Kind { get; set; } = null!;

    public int? AccountId { get; set; }

    /// <summary>The <see cref="AiImageJob"/>, when the call belonged to one (Suggest has none).</summary>
    public long? RefJobId { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    [Precision(12, 5)]
    public decimal AmountUsd { get; set; }
}
