using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// Super-admin prompt tuning per look (AI_PRODUCT_STUDIO_SPEC.md §8, phase 4). A row overrides the code defaults in
/// <c>AiStudioLooks</c>: the label the shops see, the English prompt template, the image provider, the credit price and whether
/// the look is offered. <see cref="AccountId"/> NULL = global; a per-account row wins over the global one for that account.
/// </summary>
[Index("Look", "AccountId", Name = "UQ_AiStylePreset_Look_Account", IsUnique = true)]
public partial class AiStylePreset
{
    [Key]
    public int Id { get; set; }

    public int? AccountId { get; set; }

    [StringLength(50)]
    public string Look { get; set; } = null!;

    [StringLength(100)]
    public string? Label { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    /// <summary>Prompt template with the same placeholders as the code template ({category} {ratio} {scene} {brief}). NULL = code template.</summary>
    public string? Prompt { get; set; }

    /// <summary>openai | gemini | NULL = AiStudio:DefaultProvider.</summary>
    [StringLength(20)]
    public string? Provider { get; set; }

    /// <summary>NULL = the code price.</summary>
    public int? Credits { get; set; }

    public bool IsActive { get; set; } = true;

    public int SortOrder { get; set; }

    /// <summary>JSON array of up to 3 style reference image URLs. NULL = inherit (account row → global row → AiStudio:ReferenceUrls).</summary>
    public string? ReferenceUrls { get; set; }

    /// <summary>Per-account rows: the shop's own style wishes ("תמיד על קרש עץ"), appended to the prompt. Any language.</summary>
    [StringLength(500)]
    public string? StyleNotes { get; set; }

    [Precision(0)]
    public DateTime UpdateTime { get; set; }

    public int? UpdateUserId { get; set; }
}
