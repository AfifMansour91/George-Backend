using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One AI Product Studio request (AI_PRODUCT_STUDIO_SPEC.md §8): a source photo (or none, for <c>generate</c>) and a look,
/// processed by <c>AiStudioJobRunner</c> in two stages - a fast preview the user sees within seconds and the full-size
/// final rendered in the background. Everything the shop, the super-admin and the ledger need to see later lives here:
/// which provider/model ran, the prompt, the judge's scores, token/USD cost and the user's thumbs.
/// </summary>
[Index("AccountId", "CreationTime", Name = "IX_AiImageJob_AccountId_CreationTime")]
[Index("Status", Name = "IX_AiImageJob_Status")]
[Index("GuidId", Name = "UQ_AiImageJob_GuidId", IsUnique = true)]
public partial class AiImageJob
{
    public const string StatusQueued = "queued";
    public const string StatusRunning = "running";
    public const string StatusPreviewReady = "preview_ready";
    public const string StatusDone = "done";
    public const string StatusFailed = "failed";
    public const string StatusCanceled = "canceled";

    public const string StageIdentify = "identify";
    public const string StagePreview = "preview";
    public const string StageJudge = "judge";
    public const string StageFinal = "final";

    [Key]
    public long Id { get; set; }

    /// <summary>Public id (the API never exposes <see cref="Id"/>).</summary>
    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? StartedAt { get; set; }

    [Precision(0)]
    public DateTime? FinishedAt { get; set; }

    public int AccountId { get; set; }

    public int? SiteId { get; set; }

    public int UserId { get; set; }

    public int? ProductId { get; set; }

    public int? SourceMediaId { get; set; }

    /// <summary>Where the source photo was read from (our storage or an external URL). NULL for <c>generate</c>.</summary>
    [StringLength(1000)]
    public string? SourceUrl { get; set; }

    /// <summary><c>studio_white</c> | <c>dark</c> | <c>marble</c> | <c>ambience</c> | <c>enhance</c> | <c>generate</c>.</summary>
    [StringLength(50)]
    public string Look { get; set; } = null!;

    /// <summary>Which stage the runner is in / stopped at: identify | preview | judge | final.</summary>
    [StringLength(20)]
    public string? Stage { get; set; }

    /// <summary>queued | running | preview_ready | done | failed | canceled.</summary>
    [StringLength(20)]
    public string Status { get; set; } = StatusQueued;

    [StringLength(1000)]
    public string? Error { get; set; }

    /// <summary>JSON of the request options: ratio, product name, category, scene override, provider override.</summary>
    [StringLength(2000)]
    public string? OptionsJson { get; set; }

    /// <summary>What Claude said is in the photo but is NOT the product (foil, tray, counter) - injected into the prompt.</summary>
    [StringLength(1000)]
    public string? IncidentalText { get; set; }

    [StringLength(30)]
    public string? Provider { get; set; }

    [StringLength(80)]
    public string? Model { get; set; }

    public string? Prompt { get; set; }

    [StringLength(1000)]
    public string? PreviewUrl { get; set; }

    public int? PreviewMediaId { get; set; }

    [StringLength(1000)]
    public string? ResultUrl { get; set; }

    public int? ResultMediaId { get; set; }

    public int Credits { get; set; }

    public bool Refunded { get; set; }

    /// <summary>The <see cref="AiCreditLedger"/> consumption entry that paid for this job (paid mode). NULL = free / community.</summary>
    public long? LedgerEntryId { get; set; }

    public long? RetryOfJobId { get; set; }

    [Precision(4, 3)]
    public decimal? FidelityScore { get; set; }

    [Precision(4, 3)]
    public decimal? AestheticScore { get; set; }

    [StringLength(2000)]
    public string? JudgeIssues { get; set; }

    [StringLength(20)]
    public string? JudgeVerdict { get; set; }

    public int? InputTokens { get; set; }

    public int? OutputTokens { get; set; }

    [Precision(10, 5)]
    public decimal? ProviderCostUsd { get; set; }

    /// <summary>1 = thumbs up, 0 = thumbs down, NULL = not rated.</summary>
    public byte? UserRating { get; set; }

    /// <summary>Community mode: the result was donated to the global media pool.</summary>
    public bool IsShared { get; set; }

    /// <summary>Preview generation time, ms (the number the "wow" target is measured on).</summary>
    public int? PreviewMs { get; set; }

    public int? FinalMs { get; set; }
}
