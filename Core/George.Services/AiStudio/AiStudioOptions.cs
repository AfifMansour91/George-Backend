namespace George.Services.AiStudio;

/// <summary>
/// <c>"AiStudio"</c> section of appsettings (secrets in appsettings.User.json, never committed). Model names live here, not in
/// code: gpt-image-1 was retired after a year and the next rename should be a config change.
/// </summary>
public class AiStudioOptions
{
    public const string SectionName = "AiStudio";

    public ProviderKey Gemini { get; set; } = new();
    public ProviderKey OpenAI { get; set; } = new();
    public ProviderKey Anthropic { get; set; } = new();

    /// <summary><c>openai</c> (default, won the 29.10.2026 spike) | <c>gemini</c>.</summary>
    public string DefaultProvider { get; set; } = "openai";

    /// <summary>OpenAI quality of the fast preview (~15 s): low | medium | high.</summary>
    public string PreviewQuality { get; set; } = "low";

    /// <summary>OpenAI quality of the final (~45 s).</summary>
    public string FinalQuality { get; set; } = "high";

    /// <summary>Longest edge of the preview (OpenAI custom sizes are multiples of 16).</summary>
    public int PreviewPx { get; set; } = 1024;

    /// <summary>Longest edge of the final image. 1536 tested; above 2560 OpenAI calls it experimental.</summary>
    public int FinalPx { get; set; } = 1536;

    /// <summary>Gemini image_size for the preview / final: 1K | 2K | 4K.</summary>
    public string GeminiPreviewSize { get; set; } = "1K";
    public string GeminiFinalSize { get; set; } = "2K";

    /// <summary>Longest edge of a 4K ("HD") final. OpenAI's pixel budget is 8,294,400 (2880² for a square), so this is the
    /// practical "4K-class" size; Gemini gets image_size 4K.</summary>
    public int HdPx { get; set; } = 2880;

    /// <summary>Extra credits for a 4K final (spec §7: "שדרוג ל-4K +3"). 0 = 4K costs nothing extra.</summary>
    public int HdCredits { get; set; } = 3;

    /// <summary>Workers running provider calls at the same time.</summary>
    public int MaxConcurrentJobs { get; set; } = 3;

    /// <summary>A job "running" longer than this is a crashed worker and gets failed by the sweeper.</summary>
    public int JobTimeoutSeconds { get; set; } = 300;

    /// <summary>How often an idle worker looks for queued jobs. Lower it on a local API that shares the QA database so the local
    /// worker wins the claim race against the QA server (claims are atomic, so two workers never run the same job).</summary>
    public int PollMilliseconds { get; set; } = 2000;

    /// <summary>Per-account jobs per day when Account.AiStudioDailyCap is NULL.</summary>
    public int DefaultDailyCap { get; set; } = 150;

    /// <summary>Skip the Claude identify/judge steps (no Anthropic key, or to save the ~$0.02).</summary>
    public bool UseVision { get; set; } = true;

    /// <summary>Below this judge fidelity the preview is regenerated once, on the platform's dime.</summary>
    public decimal RetryBelowFidelity { get; set; } = 0.6m;

    /// <summary>Longest edge the source photo is downscaled to before it is sent to a provider.</summary>
    public int SourceMaxPx { get; set; } = 2048;

    /// <summary>JPEG quality of stored results.</summary>
    public int OutputJpegQuality { get; set; } = 92;

    /// <summary>Anthropic list price used to estimate vision cost (claude-opus-5-5: $4 / $20 per MTok). Only an estimate -
    /// the provider wallet on /aistudioadmin anchors on the balance the admin types in.</summary>
    public decimal AnthropicInputUsdPerMTok { get; set; } = 4m;
    public decimal AnthropicOutputUsdPerMTok { get; set; } = 20m;

    /// <summary>Per-look style reference image URLs (e.g. two deliz.co.il photos for <c>dark</c>). Key = look.</summary>
    public Dictionary<string, List<string>> ReferenceUrls { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public class ProviderKey
    {
        public string? ApiKey { get; set; }
        public string? Model { get; set; }
        /// <summary>Gemini: the premium model (gemini-3-pro-image).</summary>
        public string? ProModel { get; set; }
        /// <summary>Gemini: the cheap model (gemini-nano-banana-2.1).</summary>
        public string? FastModel { get; set; }
    }
}
