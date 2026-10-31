using System.ComponentModel.DataAnnotations;

namespace George.DB;

/// <summary>AI Product Studio columns on Media (AiStudio_Phase1_Install.sql).</summary>
public partial class Media
{
    public const string OriginUpload = "upload";
    public const string OriginAi = "ai";
    public const string OriginAiGenerated = "ai_generated";

    /// <summary><c>upload</c> | <c>ai</c> (edited from a shop photo) | <c>ai_generated</c> (no source photo) | <c>import</c>. NULL = legacy upload.</summary>
    [StringLength(20)]
    public string? Origin { get; set; }

    /// <summary>The <see cref="AiImageJob"/> that produced this file.</summary>
    public long? AiJobId { get; set; }

    /// <summary>Community mode: donated to the shared pool (visible in the global library to every account).</summary>
    public bool LicensePool { get; set; }
}
