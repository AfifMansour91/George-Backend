namespace George.Services.Response;

/// <summary>Kiosk design and behavior settings (matches frontend KioskSettings type).</summary>
public class KioskSettingsRes
{
    public string? KioskLogoUrl { get; set; }
    public string? HeaderBgColor { get; set; }
    public string? HomeBgType { get; set; }
    /// <summary>Media ID for home background video (FK to Media).</summary>
    public int? HomeVideoMediaId { get; set; }
    /// <summary>Resolved URL from Media table for display.</summary>
    public string? HomeVideoUrl { get; set; }
    /// <summary>Media IDs for rotating home images (FKs to Media), in sort order.</summary>
    public List<int>? HomeImageMediaIds { get; set; }
    /// <summary>Resolved URLs from Media table for display.</summary>
    public List<string>? HomeImageUrls { get; set; }
    public int? HomeImageIntervalSeconds { get; set; }
    public string? PrimaryColor { get; set; }
    public string? SecondaryColor { get; set; }
    public string? PosProductsTitle { get; set; }
    /// <summary>"upsells" | "category" | "combined"</summary>
    public string? PosProductsType { get; set; }
    public int? PosProductsCategoryId { get; set; }
    public bool CreditEnabled { get; set; }
    public bool CashAtRegisterEnabled { get; set; }
}
