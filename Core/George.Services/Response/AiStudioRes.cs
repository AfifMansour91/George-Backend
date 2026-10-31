namespace George.Services.Response;

/// <summary>GET /AiStudio/Status - is the studio on for this account, which looks exist, what they cost, how much was used today.</summary>
public class AiStudioStatusRes
{
    public bool Enabled { get; set; }
    public int AccountId { get; set; }
    /// <summary>community | paid.</summary>
    public string Mode { get; set; } = "community";
    public int DailyCap { get; set; }
    public int UsedToday { get; set; }
    public bool VisionEnabled { get; set; }
    public string DefaultProvider { get; set; } = "openai";
    public List<AiLookRes> Looks { get; set; } = new();
    /// <summary>Paid mode: credits left. Community mode: 0 (nothing is charged).</summary>
    public int Balance { get; set; }
    /// <summary>The platform terminal is configured, so paid-mode shops can buy packages online.</summary>
    public bool CanPurchase { get; set; }
    /// <summary>Community mode: when the shop accepted the pool terms; null = the consent dialog must be shown before the first job.</summary>
    public DateTime? PoolConsentAt { get; set; }
    public bool RequiresPoolConsent { get; set; }
    /// <summary>Extra credits for a 4K final; 0 = the option is off on this server.</summary>
    public int HdCredits { get; set; }
    /// <summary>The site's defaults (Settings › סטודיו AI › ברירות מחדל): which look starts automatically, ratio, 2k/4k.</summary>
    public AiStudioDefaultsRes Defaults { get; set; } = new();
}

/// <summary>Per-site AI defaults (Site.AiDefaultLook / AiImageRatio / AiFinalSize), resolved for the site the status was asked for.</summary>
public class AiStudioDefaultsRes
{
    public int? SiteId { get; set; }
    /// <summary>studio_white | dark | marble | ambience | enhance - always one of the active looks.</summary>
    public string Look { get; set; } = "studio_white";
    /// <summary>True when the site set a default look (false = platform fallback, the UI may prefer the user's last choice).</summary>
    public bool LookExplicit { get; set; }
    public string Ratio { get; set; } = "1:1";
    /// <summary>2k | 4k.</summary>
    public string FinalSize { get; set; } = "2k";
}

public class AiLookRes
{
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool NeedsSource { get; set; }
    /// <summary>True for the looks that can be the background of a generated (no-photo) image.</summary>
    public bool IsBackground { get; set; }
}

//*************************    Admin: looks / presets (phase 4)    *************************//

/// <summary>GET /AiStudio/Admin/Looks - one row per look: the code default, the saved override and the period's quality numbers.</summary>
public class AiLookAdminRes
{
    public string Key { get; set; } = string.Empty;
    public bool NeedsSource { get; set; }
    /// <summary>What the shops see now (code default with the override applied).</summary>
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public int Credits { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    /// <summary>The code defaults, so the editor can show "שחזר ברירת מחדל" and diff.</summary>
    public string DefaultLabel { get; set; } = string.Empty;
    public string DefaultDescription { get; set; } = string.Empty;
    public string DefaultPrompt { get; set; } = string.Empty;
    public int DefaultCredits { get; set; }
    /// <summary>True when any field is overridden by a saved row.</summary>
    public bool HasOverride { get; set; }
    public DateTime? UpdatedAt { get; set; }
    // ---- period stats ----
    public int Jobs { get; set; }
    public int Done { get; set; }
    public int Failed { get; set; }
    public int Retries { get; set; }
    public decimal? AvgFidelity { get; set; }
    public decimal? AvgAesthetic { get; set; }
    public int ThumbsUp { get; set; }
    public int ThumbsDown { get; set; }
    public decimal CostUsd { get; set; }
    public int? AvgPreviewMs { get; set; }
    /// <summary>The judge's most frequent complaints in the period, most common first.</summary>
    public List<AiLookIssueRes> TopIssues { get; set; } = new();
}

public class AiLookIssueRes
{
    public string Text { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class AiLooksAdminRes
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string DefaultProvider { get; set; } = "openai";
    public List<string> Providers { get; set; } = new();
    public int HdCredits { get; set; }
    public List<AiLookAdminRes> Looks { get; set; } = new();
}

/// <summary>POST /AiStudio/Suggest - what Claude read off the photo. Everything is a suggestion the shop can edit.</summary>
public class AiSuggestRes
{
    public string? Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    /// <summary>One of the names the caller sent, or null.</summary>
    public string? CategoryName { get; set; }
    public string? BrandName { get; set; }
    public bool IsPackaged { get; set; }
    public bool IsWeighted { get; set; }
    /// <summary>Weighted product: up to 4 per-unit weights in kg, ascending.</summary>
    public List<decimal> WeightOptionsKg { get; set; } = new();
    /// <summary>Packaged product: net weight in kg from the label.</summary>
    public decimal? UnitWeightKg { get; set; }
    public int Credits { get; set; }
}

/// <summary>The shop's view of a job. Also the SignalR <c>AiJobUpdated</c> payload.</summary>
public class AiJobRes
{
    public Guid Id { get; set; }
    public DateTime CreationTime { get; set; }
    public int AccountId { get; set; }
    public int? SiteId { get; set; }
    public int? ProductId { get; set; }
    public string Look { get; set; } = string.Empty;
    public string? LookLabel { get; set; }
    /// <summary><c>generate</c> jobs: the background look that was drawn (studio_white | dark | marble | ambience) and its label.</summary>
    public string? Background { get; set; }
    public string? BackgroundLabel { get; set; }
    /// <summary>queued | running | preview_ready | done | failed | canceled.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>identify | preview | judge | final.</summary>
    public string? Stage { get; set; }
    public string? Error { get; set; }
    public string? SourceUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? ResultUrl { get; set; }
    public int? ResultMediaId { get; set; }
    public int Credits { get; set; }
    /// <summary>True when the job failed and nothing is charged (shown as "לא חויב").</summary>
    public bool Refunded { get; set; }
    public bool IsRetry { get; set; }
    public decimal? FidelityScore { get; set; }
    public decimal? AestheticScore { get; set; }
    public string? JudgeVerdict { get; set; }
    public List<string> JudgeIssues { get; set; } = new();
    public byte? UserRating { get; set; }
    public bool IsShared { get; set; }
    public int? PreviewMs { get; set; }
    public int? FinalMs { get; set; }
    public string? Provider { get; set; }
    /// <summary>What Claude recognised (shown as a hint: "זיהינו: שוקיים עוף").</summary>
    public string? IdentifiedName { get; set; }
    /// <summary>The final is rendered at 4K.</summary>
    public bool Hd { get; set; }
}

//*************************    Credits (phase 3)    *************************//

public class AiCreditLedgerEntryRes
{
    public long Id { get; set; }
    public DateTime CreationTime { get; set; }
    /// <summary>allocation | purchase | consumption | refund.</summary>
    public string EntryType { get; set; } = string.Empty;
    public int Amount { get; set; }
    public long? RefJobId { get; set; }
    public string? Note { get; set; }
}

/// <summary>GET /AiStudio/Credits - the shop's balance and history.</summary>
public class AiCreditsRes
{
    public int AccountId { get; set; }
    public string Mode { get; set; } = "community";
    public int Balance { get; set; }
    public int Purchased { get; set; }
    public int Consumed { get; set; }
    public int Refunded { get; set; }
    public int ConsumedLast30Days { get; set; }
    public DateTime? LastPurchaseAt { get; set; }
    public bool CanPurchase { get; set; }
    public DateTime? PoolConsentAt { get; set; }
    public List<AiCreditLedgerEntryRes> Ledger { get; set; } = new();
    public List<AiCreditPurchaseRes> Purchases { get; set; } = new();
}

public class AiCreditPackageRes
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Credits { get; set; }
    /// <summary>Before VAT, as in the price list.</summary>
    public decimal Price { get; set; }
    public decimal VatPercent { get; set; }
    /// <summary>Price + VAT, rounded to agorot.</summary>
    public decimal Total { get; set; }
    /// <summary>Agorot per credit before VAT.</summary>
    public decimal PricePerCreditAgorot { get; set; }
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public string? Badge { get; set; }
}

public class AiCreditPackagesRes
{
    public List<AiCreditPackageRes> Packages { get; set; } = new();
    public decimal VatPercent { get; set; }
    /// <summary>False = the platform terminal is not configured; the list is for display only.</summary>
    public bool CanPurchase { get; set; }
    /// <summary>What each look costs, so the package picker can say "300 קרדיטים = 75 תמונות".</summary>
    public List<AiLookRes> Looks { get; set; } = new();
}

public class AiCreditPurchaseStartRes
{
    public int PurchaseId { get; set; }
    /// <summary>Cardcom hosted payment page - the browser navigates there.</summary>
    public string PaymentUrl { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

public class AiCreditPurchaseRes
{
    public int Id { get; set; }
    public DateTime CreationTime { get; set; }
    public int? PackageId { get; set; }
    public string? PackageName { get; set; }
    public int CreditsAmount { get; set; }
    public decimal Price { get; set; }
    public decimal VatPercent { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "ILS";
    /// <summary>initiated | paid | failed | canceled.</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public string? CardLast4 { get; set; }
    public string? CardBrand { get; set; }
    public string? DocumentNumber { get; set; }
    public string? DocumentUrl { get; set; }
    public string? ErrorDescription { get; set; }
    /// <summary>The balance after this purchase - filled on the confirm call so the return page can show it.</summary>
    public AiCreditsRes? Credits { get; set; }
}

/// <summary>Super-admin AI studio screen: one row per account.</summary>
public class AiStudioAdminAccountRes
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool AiStudioEnabled { get; set; }
    public string Mode { get; set; } = "community";
    public int? DailyCap { get; set; }
    public DateTime? PoolConsentAt { get; set; }
    public int JobsInPeriod { get; set; }
    public int DoneInPeriod { get; set; }
    public int FailedInPeriod { get; set; }
    public int SharedInPeriod { get; set; }
    public int JobsTotal { get; set; }
    public decimal ProviderCostUsdInPeriod { get; set; }
    public decimal ProviderCostUsdTotal { get; set; }
    public decimal? AvgFidelityInPeriod { get; set; }
    public int ThumbsUpInPeriod { get; set; }
    public int ThumbsDownInPeriod { get; set; }
    public DateTime? LastJobAt { get; set; }
    public int CreditsConsumedInPeriod { get; set; }
    public int CreditsBalance { get; set; }
    public int PurchasesInPeriod { get; set; }
    public decimal RevenueInPeriod { get; set; }
    public int PurchasesTotal { get; set; }
    public decimal RevenueTotal { get; set; }
    public DateTime? LastPurchaseAt { get; set; }
}

public class AiStudioAdminOverviewRes
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public bool PurchaseConfigured { get; set; }
    public int JobsInPeriod { get; set; }
    public int DoneInPeriod { get; set; }
    public int FailedInPeriod { get; set; }
    public int SharedInPeriod { get; set; }
    public decimal ProviderCostUsdInPeriod { get; set; }
    public int PurchasesInPeriod { get; set; }
    public decimal RevenueInPeriod { get; set; }
    public decimal RevenueTotal { get; set; }
    public int ActiveAccounts { get; set; }
    public List<AiStudioAdminAccountRes> Accounts { get; set; } = new();
    /// <summary>Spend and estimated remaining balance per provider (OpenAI / Gemini / Anthropic).</summary>
    public List<AiProviderWalletRes> Providers { get; set; } = new();
}

/// <summary>One provider's wallet on the super-admin page: our spend journal against the balance the admin typed in.</summary>
public class AiProviderWalletRes
{
    public string Provider { get; set; } = null!;
    public string Label { get; set; } = null!;
    /// <summary>Where to load money / read the real balance.</summary>
    public string ConsoleUrl { get; set; } = null!;
    public bool Configured { get; set; }
    public decimal SpentInPeriod { get; set; }
    public int CallsInPeriod { get; set; }
    public decimal SpentLast30Days { get; set; }
    public decimal SpentTotal { get; set; }
    public DateTime? LastCallAt { get; set; }
    /// <summary>Balance as typed on the last top-up; null = never entered.</summary>
    public decimal? BalanceEnteredUsd { get; set; }
    public DateTime? BalanceEnteredAt { get; set; }
    public string? BalanceNote { get; set; }
    public decimal? SpentSinceEntered { get; set; }
    /// <summary>BalanceEnteredUsd - SpentSinceEntered; null when no balance was entered.</summary>
    public decimal? RemainingEstimateUsd { get; set; }
    public decimal AlertBelowUsd { get; set; }
    /// <summary>Average USD per day over the trailing 30 days.</summary>
    public decimal BurnPerDayUsd { get; set; }
    /// <summary>RemainingEstimate / BurnPerDay; null when either is unknown.</summary>
    public int? DaysLeft { get; set; }
    /// <summary>ok | low | empty | unknown.</summary>
    public string Level { get; set; } = "unknown";
}

public class AiStudioAdminJobsRes
{
    public List<AiStudioAdminJobRes> Items { get; set; } = new();
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}

/// <summary>Super-admin job log row: the shop's view of the job plus what only the platform sees (model, tokens, cost).</summary>
public class AiStudioAdminJobRes
{
    public AiJobRes Job { get; set; } = null!;
    public string AccountName { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? Model { get; set; }
    public int? InputTokens { get; set; }
    public int? OutputTokens { get; set; }
    public decimal? ProviderCostUsd { get; set; }
    public string? Error { get; set; }
}
