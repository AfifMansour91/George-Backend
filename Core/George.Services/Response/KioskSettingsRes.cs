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
    /// <summary>Show "הזמנה חוזרת" (Repeat Order) button in kiosk (default false).</summary>
    public bool ShowDuplicateOrderButton { get; set; } = false;
    /// <summary>Enable POS products (upsell) step (default true).</summary>
    public bool PosProductsEnabled { get; set; } = true;
    /// <summary>Button text: "To payment / View order".</summary>
    public string? ButtonTextToPaymentOrViewOrder { get; set; }
    /// <summary>Button text: "To payment" (cart).</summary>
    public string? ButtonTextCartToPayment { get; set; }
    /// <summary>Button text: "Continue to payment" (upsell).</summary>
    public string? ButtonTextUpsellContinueToPayment { get; set; }
    /// <summary>Seconds before "Are you still there?" popup (default 60).</summary>
    public int? InactivityPopupSeconds { get; set; }
    /// <summary>When true, privacy policy checkbox on phone screen is checked by default.</summary>
    public bool PrivacyPolicyCheckboxCheckedByDefault { get; set; } = false;
    /// <summary>Privacy policy content (HTML or plain text); shown in panel when user clicks the link.</summary>
    public string? PrivacyPolicyContent { get; set; }
    /// <summary>Product card image aspect ratio: "3:2" or "1:1".</summary>
    public string? ProductImageAspectRatio { get; set; }
}
