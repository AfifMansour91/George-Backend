namespace George.Services.Request;

/// <summary>Scope of an AI Studio call: the account is the caller's own; a master/admin may name another one.</summary>
public class AiStudioScopeReq
{
    public int? AccountId { get; set; }
    public int? SiteId { get; set; }
}

/// <summary>POST /AiStudio/Jobs - one look on one photo (or no photo for <c>generate</c>).</summary>
public class AiJobCreateReq : AiStudioScopeReq
{
    /// <summary>studio_white | dark | marble | ambience | enhance | generate.</summary>
    public string Look { get; set; } = string.Empty;

    /// <summary>The photo to work on - a URL in our storage (from /General/File/Upload) or any https image. Required unless Look = generate.</summary>
    public string? SourceUrl { get; set; }
    public int? SourceMediaId { get; set; }
    public int? ProductId { get; set; }

    /// <summary>Hints for Claude and the prompt. For <c>generate</c> the description is what the image must show (HTML is stripped).</summary>
    public string? ProductName { get; set; }
    public string? CategoryName { get; set; }
    public string? Description { get; set; }

    /// <summary><c>generate</c> only: the background look - studio_white (default) | dark | marble | ambience.</summary>
    public string? Background { get; set; }

    /// <summary>1:1 (default) | 4:5 | 3:4 | 4:3 | 16:9.</summary>
    public string? Ratio { get; set; }

    /// <summary>Ambience look: free-text scene instead of the category table.</summary>
    public string? Scene { get; set; }

    /// <summary>openai | gemini - super-admin A/B only; shops get the preset's provider.</summary>
    public string? Provider { get; set; }

    /// <summary>Community mode: the shop agreed to donate this result to the pool (first-time consent is in the UI).</summary>
    public bool? Share { get; set; }

    /// <summary>4K final (+AiStudio:HdCredits). null = the site's AiFinalSize default.</summary>
    public bool? Hd { get; set; }

    /// <summary>True when the source is a photo the user just took (quick product): follows the account's mode. Anything else
    /// (an image the shop already had - gallery ✨, media-library batch) is charged from credits in every mode
    /// (AiStudio:ExistingImageAlwaysPaid). The server verifies the claim against the Media row's age and uploader.</summary>
    public bool? Capture { get; set; }

    /// <summary><c>generate</c> with <see cref="ProductId"/>: when the final lands, set it as the product's image if the product still has none.</summary>
    public bool? AttachToProduct { get; set; }
}

/// <summary>POST /AiStudio/Jobs/ForProducts - "צור תמונות למוצרים ללא תמונה": one generate job per product (name + category +
/// short description → image), attached to the product when done. Products that already have an image are skipped.</summary>
public class AiJobsForProductsReq : AiStudioScopeReq
{
    public List<int> ProductIds { get; set; } = new();
    /// <summary>studio_white (default) | dark | marble | ambience.</summary>
    public string? Background { get; set; }
    public bool? Hd { get; set; }
}

/// <summary>PUT /AiStudio/Presets - the shop's own settings of one look (הגדרות › סטודיו AI › המראים שלי).</summary>
public class AiAccountLookSaveReq : AiStudioScopeReq
{
    public string Look { get; set; } = string.Empty;
    /// <summary>False hides the look from this shop's dialogs.</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Free text appended to the prompt ("תמיד על קרש עץ עם רוזמרין"), up to 500 chars, any language.</summary>
    public string? StyleNotes { get; set; }
    /// <summary>Up to 3 image URLs (from the shop's media library) the model copies the STYLE of. Empty = the platform's references.</summary>
    public List<string>? ReferenceUrls { get; set; }
}

/// <summary>PUT /AiStudio/Admin/Looks - super-admin: the global preset of one look. Empty fields = back to the code default.</summary>
public class AiLookPresetSaveReq
{
    /// <summary>Up to 3 style reference image URLs; empty = the server's AiStudio:ReferenceUrls for this look.</summary>
    public List<string>? ReferenceUrls { get; set; }
    public string Look { get; set; } = string.Empty;
    public string? Label { get; set; }
    public string? Description { get; set; }
    /// <summary>English prompt template; placeholders {category} {ratio} {scene} ({brief} for generate). Empty = code template.</summary>
    public string? Prompt { get; set; }
    /// <summary>openai | gemini | empty = AiStudio:DefaultProvider.</summary>
    public string? Provider { get; set; }
    /// <summary>null = code price.</summary>
    public int? Credits { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class AiJobRateReq : AiStudioScopeReq
{
    /// <summary>1 = 👍, 0 = 👎, null = clear.</summary>
    public byte? Rating { get; set; }
}

/// <summary>POST /AiStudio/Suggest - product copy from a photo ("כתוב ב-AI" in the quick-product flow). Synchronous.</summary>
public class AiSuggestReq : AiStudioScopeReq
{
    /// <summary>The photo, as uploaded through /General/File/Upload.</summary>
    public string SourceUrl { get; set; } = string.Empty;
    /// <summary>What the shop typed so far; Claude keeps and polishes it.</summary>
    public string? ProductName { get; set; }
    public string? CategoryName { get; set; }
    /// <summary>The shop's category names; Claude picks one of them verbatim.</summary>
    public List<string>? Categories { get; set; }
    public string? Barcode { get; set; }
    /// <summary>Look the barcode up on the web (EAN/UPC of 8+ digits only). null = yes when such a barcode was sent. Costs
    /// AiStudio:WebSearchCredits instead of 1.</summary>
    public bool? WebSearch { get; set; }
}

/// <summary>POST /AiStudio/Purchases - open the hosted payment page for one credit package.</summary>
public class AiCreditPurchaseStartReq
{
    public int? AccountId { get; set; }
    public int PackageId { get; set; }
    /// <summary>The browser origin the shop is on; the hosted page returns there (falls back to App:PublicBaseUrl).</summary>
    public string? AppOrigin { get; set; }
}

/// <summary>Super-admin: one catalog row (id 0 = new; rows missing from the saved list are deactivated).</summary>
public class AiCreditPackageSaveReq
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Credits { get; set; }
    /// <summary>ILS before VAT.</summary>
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public string? Badge { get; set; }
}

/// <summary>Super-admin: load (or correct) a shop's credits by hand.</summary>
public class AiCreditAllocateReq
{
    public int Amount { get; set; }
    public string? Note { get; set; }
}

/// <summary>Super-admin: "this is the balance I see in the provider's console right now".</summary>
public class AiProviderTopUpReq
{
    /// <summary>openai | gemini | anthropic.</summary>
    public string Provider { get; set; } = null!;
    public decimal BalanceUsd { get; set; }
    /// <summary>Warn under this on the admin page; null = default (20).</summary>
    public decimal? AlertBelowUsd { get; set; }
    public string? Note { get; set; }
}

public class AiJobListReq : AiStudioScopeReq
{
    public int? ProductId { get; set; }
    public int Take { get; set; } = 20;
}
