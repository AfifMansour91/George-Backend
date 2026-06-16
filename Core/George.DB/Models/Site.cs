using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class Site
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    public bool IsActive { get; set; }

    public int AccountId { get; set; }

    [StringLength(200)]
    public string SiteName { get; set; } = null!;

    [StringLength(500)]
    public string? Location { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(20)]
    public string? Status { get; set; }

    [StringLength(250)]
    public string? ContactEmail { get; set; }

    [StringLength(50)]
    public string? ContactPhone { get; set; }

    public bool? IsKosherSite { get; set; }

    public bool? AllowWeightedProducts { get; set; }

    [StringLength(10)]
    public string Currency { get; set; } = null!;

    /// <summary>WooCommerce site root (wc/v3 product REST and oc-storeos plugin: orders + closing-dates under the same host).</summary>
    [StringLength(500)]
    public string? WooCommerceUrl { get; set; }

    [StringLength(250)]
    public string? WooCommerceKey { get; set; }

    [StringLength(250)]
    public string? WooCommerceSecret { get; set; }

    public bool? WooCommerceEnabled { get; set; }

    /// <summary>Bearer token for OC Wolt Drive dispatch API (<c>POST .../wp-json/ocws-wolt/v1/dispatch</c>). Generated in WP admin → Wolt Drive → Webhook.</summary>
    [StringLength(500)]
    public string? WoltDispatchToken { get; set; }

    /// <summary>When true, Wolt dispatch UI and API are enabled for this site (requires plugin probe + token).</summary>
    public bool? WoltEnabled { get; set; }

    /// <summary>API key for WooCommerce to call our APIs (X-Api-Key header). Per-site.</summary>
    [StringLength(100)]
    public string? InternalApiKey { get; set; }

    public int? WeightTolerancePercent { get; set; }

    public bool? DepreciationEnabled { get; set; }

    [StringLength(200)]
    public string? DepreciationPercentagesJson { get; set; }

    public int? PrepTimeMinutes { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ShippingCost { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? FreeShippingAbove { get; set; }

    /// <summary>When true, manual/phone order shipping address uses searchable Israel city list. Default true for new sites.</summary>
    public bool? IsraelCityPickerEnabled { get; set; }

    public bool? AutoPrintEnabled { get; set; }

    public bool? PrintNewOrderImmediate { get; set; }

    public bool? PrintMovedToTreatment { get; set; }

    /// <summary>When true, print voucher automatically when picking is completed (with weights and final price).</summary>
    public bool? PrintAfterPicking { get; set; }

    public bool? PrintFutureImmediate { get; set; }

    public bool? PrintFutureAtTimeEnabled { get; set; }

    [StringLength(10)]
    public string? PrintFutureAtTime { get; set; }

    /// <summary>Voucher printer: print without showing confirmation (use default printer).</summary>
    public bool? VoucherPrinterSilent { get; set; }

    /// <summary>Voucher printer: display name / connection label (e.g. WIFI, iPad).</summary>
    [StringLength(100)]
    public string? VoucherPrinterName { get; set; }

    /// <summary>When true (default), send voucher jobs to local PrintAgent instead of browser print.</summary>
    public bool VoucherPrinterUseAgent { get; set; } = true;

    /// <summary>Label printer: display name / connection label (optional second printer at branch).</summary>
    [StringLength(100)]
    public string? LabelPrinterName { get; set; }

    /// <summary>When true (default), send label jobs to local PrintAgent.</summary>
    public bool LabelPrinterUseAgent { get; set; } = true;

    /// <summary>When true, print bag labels automatically when picking is completed.</summary>
    public bool? PrintLabelsAfterPicking { get; set; }

    /// <summary>
    /// Promotion settings (Sprint 4 — `Sprint4/מבצעים.md` "הגדרות מבצעים").
    /// Default for BxPY over-quantity pricing: "same_price" (pro-rated) | "full_price" (regular). Null = "full_price".
    /// </summary>
    [StringLength(20)]
    public string? PromotionOveragePolicyDefault { get; set; }

    /// <summary>When true (default), promotions apply to phone-placed manual orders. Spec: "הזמנות טלפוניות".</summary>
    public bool? PromotionsApplyToPhoneOrders { get; set; }

    /// <summary>When true, promotions stack on already-discounted products. Default false. Spec: "מבצע על מוצרים מוזלים".</summary>
    public bool? PromotionsApplyToDiscountedProducts { get; set; }

    /// <summary>
    /// Optional URL that StoreOS POSTs promotion lifecycle events to (created/updated/ended).
    /// See `Sprint4/מבצעים.md` "סנכרון מבצעים לאתר ולקיוסק (Webhook)".
    /// </summary>
    [StringLength(500)]
    public string? PromotionWebhookUrl { get; set; }

    /// <summary>Shared secret for HMAC-SHA256 signing of webhook bodies (header `X-StoreOS-Signature`).</summary>
    [StringLength(200)]
    public string? PromotionWebhookSecret { get; set; }

    /// <summary>When true (default), staff must confirm bag count before completing picking. When false, skip that prompt.</summary>
    public bool? AskBagsCountAtPickingFinish { get; set; }

    /// <summary>Active payment provider: none | cardcom.</summary>
    [StringLength(32)]
    public string PaymentGatewayProvider { get; set; } = "none";

    public int? CardcomTerminalNumber { get; set; }

    [StringLength(100)]
    public string? CardcomApiName { get; set; }

    [StringLength(500)]
    public string? CardcomApiPasswordEncrypted { get; set; }

    public bool CardcomSaveCardEnabled { get; set; } = true;

    public int PaymentAuthBufferPercent { get; set; } = 25;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? PaymentMaxAuthAmount { get; set; }

    public bool PaymentAllowCaptureAboveAuth { get; set; }

    [StringLength(2000)]
    public string? CardcomProviderExtrasJson { get; set; }

    [StringLength(500)]
    public string? CardcomCssUrl { get; set; }

    [StringLength(500)]
    public string? CardcomLogoUrl { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Site")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Site")]
    public virtual ICollection<AccountMedia> AccountMedia { get; set; } = new List<AccountMedia>();

    [InverseProperty("Site")]
    public virtual ICollection<AccountWizardStepData> AccountWizardStepData { get; set; } = new List<AccountWizardStepData>();

    [InverseProperty("Site")]
    public virtual ICollection<Attribute> Attribute { get; set; } = new List<Attribute>();

    [ForeignKey("CreationUserId")]
    [InverseProperty("SiteCreationUser")]
    public virtual User? CreationUser { get; set; }

    [InverseProperty("Site")]
    public virtual ICollection<Order> Order { get; set; } = new List<Order>();

    [InverseProperty("Site")]
    public virtual ICollection<Promotion> Promotion { get; set; } = new List<Promotion>();

    [InverseProperty("Site")]
    public virtual ICollection<CustomerPaymentMethod> CustomerPaymentMethod { get; set; } = new List<CustomerPaymentMethod>();

    [InverseProperty("Site")]
    public virtual ICollection<SiteOrderReceptionClosed> SiteOrderReceptionClosed { get; set; } = new List<SiteOrderReceptionClosed>();

    [ForeignKey("UpdateUserId")]
    [InverseProperty("SiteUpdateUser")]
    public virtual User? UpdateUser { get; set; }

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<Brand> Brand { get; set; } = new List<Brand>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<BusinessType> BusinessType { get; set; } = new List<BusinessType>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<Category> Category { get; set; } = new List<Category>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<Product> Product { get; set; } = new List<Product>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<TemplateAttribute> TemplateAttribute { get; set; } = new List<TemplateAttribute>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<TemplateProduct> TemplateProduct { get; set; } = new List<TemplateProduct>();

    [ForeignKey("SiteId")]
    [InverseProperty("Site")]
    public virtual ICollection<User> User { get; set; } = new List<User>();
}
