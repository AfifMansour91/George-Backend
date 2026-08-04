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

    /// <summary>Customer-behavior: a customer with a single order is considered inactive / at-risk-of-churn after this many days without an order. Customers with order history use their personal cadence (avg gap × 2). Default 14.</summary>
    public int? CustomerInactiveAfterDays { get; set; }

    /// <summary>Customer-behavior: when true (default), customer KPI/statistics include orders that haven't been completed yet; when false, only completed orders count.</summary>
    public bool? IncludeIncompleteOrdersInStats { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ShippingCost { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? FreeShippingAbove { get; set; }

    /// <summary>When true, manual/phone order shipping address uses searchable Israel city list. Default true for new sites.</summary>
    public bool? IsraelCityPickerEnabled { get; set; }

    public bool? AutoPrintEnabled { get; set; }

    /// <summary>
    /// When true, order printouts (manual + auto) use a full A4 page layout instead of the thermal
    /// voucher (בון). Auto-print jobs are delivered to the PrintAgent as an A4 PDF; manual prints
    /// from the browser use the A4 template.
    /// </summary>
    public bool? VoucherPrintA4 { get; set; }

    /// <summary>When true, order printouts (thermal voucher + A4, manual + auto) omit the delivery/pickup time — only the date is printed.</summary>
    public bool? VoucherHideDeliveryTime { get; set; }

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

    /// <summary>When true (default), show picking confirmation dialog after barcode scan. When false, apply scan silently unless weight exceeds tolerance.</summary>
    public bool? ConfirmPickingAfterScan { get; set; }

    /// <summary>When true (default), show order deviation % and amount in picking footer and archive.</summary>
    public bool? ShowPickingDeviation { get; set; }

    /// <summary>When true, show the product SKU (מק"ט) on picking-screen lines. Off by default.</summary>
    public bool? ShowSkuInPicking { get; set; }

    /// <summary>
    /// When true, hide the per-unit / size approximate weight (e.g. "(כ 3 ק"ג)", "700 גרם ליח'") on
    /// printouts, order cards and picking lines. Customer-chosen weights (בחירת משקל ליחידה) always show.
    /// NULL/0 = show (current behavior).
    /// </summary>
    public bool? HideUnitWeightInOrders { get; set; }

    /// <summary>
    /// When true, order printouts (voucher/A4) include the permanent customer note (הערה קבועה,
    /// Customer.Notes) in the order-notes line. NULL/0 = not printed (default).
    /// </summary>
    public bool? ShowCustomerProfileNoteInPrints { get; set; }

    /// <summary>
    /// When true, "fast scan" picking mode: scans apply immediately without a confirmation dialog
    /// (overrides ConfirmPickingAfterScan), and when the last item is picked the order finishes
    /// (moves to Ready) automatically. Off by default.
    /// </summary>
    public bool? FastPickingScan { get; set; }

    /// <summary>
    /// When true (default), the deviation ("חריגה") popup is shown when a scanned quantity/weight
    /// exceeds tolerance. When false, over-tolerance scans are applied silently with no popup.
    /// </summary>
    public bool? ShowPickingExceptionsPopup { get; set; }

    /// <summary>When true, this branch uses a connected RS232 scale (ScaleAgent → live weight in picking). Off by default.</summary>
    public bool? ScaleEnabled { get; set; }

    /// <summary>
    /// How the 5-digit payload on 13-digit scale barcodes (prefix 2) is interpreted when scanning during picking:
    /// auto (detect), weight (÷1000 kg), or price (÷100 currency then ÷ catalog ₪/kg).
    /// </summary>
    [StringLength(16)]
    public string? ScaleBarcodeEmbedMode { get; set; }

    /// <summary>
    /// When true, product/variation prices on this site's WooCommerce store are managed by an external
    /// system (e.g. the POS pushes prices directly to Woo). George then never writes price fields on Woo
    /// UPDATES (creates still seed an initial price) and the daily price-pull job imports Woo prices back
    /// into George for this site. Off by default (NULL/0).
    /// </summary>
    public bool? ExternalPriceManagement { get; set; }

    /// <summary>Active payment provider: none | cardcom.</summary>
    [StringLength(32)]
    public string PaymentGatewayProvider { get; set; } = "none";

    public int? CardcomTerminalNumber { get; set; }

    /// <summary>
    /// Optional SECOND Cardcom terminal — configured at Cardcom WITHOUT a CVV requirement — used ONLY for the
    /// actual charge (J4 capture / direct token charge) and its refund. Token creation, authorization holds
    /// (J5) and their voids, and the hosted payment page all stay on the primary
    /// <see cref="CardcomTerminalNumber"/>. Null/0 = single-terminal setup, everything on the primary.
    /// </summary>
    public int? CardcomChargeTerminalNumber { get; set; }

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
