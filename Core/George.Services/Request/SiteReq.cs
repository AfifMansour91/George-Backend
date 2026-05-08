using George.Common;
using System.ComponentModel.DataAnnotations;

namespace George.Services.Request
{
    public class SiteReq
    {
        [Required]
        public int AccountId { get; set; }
        public string? SiteName { get; set; } = null!;
        
        public string? Location { get; set; }
        public string? Description { get; set; }
        public List<int>? BusinessTypeIds { get; set; }
        public string? Status { get; set; } // "active" | "inactive"
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
        public bool? IsKosherSite { get; set; }
        public bool? AllowWeightedProducts { get; set; }
        public string Currency { get; set; } = "ILS";
        public string? WooCommerceUrl { get; set; }
        public string? WooCommerceKey { get; set; }
        public string? WooCommerceSecret { get; set; }
        public bool? WooCommerceEnabled { get; set; }
        /// <summary>Internal API key for external integrations (e.g. WooCommerce plugin) to call our Order/OrderPayment APIs. Set manually or use GenerateSiteApiKey.</summary>
        public string? InternalApiKey { get; set; }

        // Shop settings (Sprint 2)
        public int? WeightTolerancePercent { get; set; }
        public bool? DepreciationEnabled { get; set; }
        public string? DepreciationPercentagesJson { get; set; }
        public int? PrepTimeMinutes { get; set; }
        public decimal? ShippingCost { get; set; }
        public decimal? FreeShippingAbove { get; set; }
        /// <summary>When true, manual/phone order uses searchable Israel city picker (default true).</summary>
        public bool? IsraelCityPickerEnabled { get; set; }
        public bool? AutoPrintEnabled { get; set; }
        public bool? PrintNewOrderImmediate { get; set; }
        public bool? PrintMovedToTreatment { get; set; }
        public bool? PrintAfterPicking { get; set; }
        public bool? PrintFutureImmediate { get; set; }
        public bool? PrintFutureAtTimeEnabled { get; set; }
        public string? PrintFutureAtTime { get; set; }
        /// <summary>Voucher printer: print without confirmation (use default printer).</summary>
        public bool? VoucherPrinterSilent { get; set; }
        /// <summary>Voucher printer: display name (e.g. WIFI, iPad).</summary>
        public string? VoucherPrinterName { get; set; }

        // Promotion settings (Sprint 4)
        /// <summary>"same_price" | "full_price" (default). Default for BxPY over-quantity pricing.</summary>
        public string? PromotionOveragePolicyDefault { get; set; }
        public bool? PromotionsApplyToPhoneOrders { get; set; }
        public bool? PromotionsApplyToDiscountedProducts { get; set; }
        /// <summary>URL the storefront/kiosk listens on for promotion lifecycle events.</summary>
        public string? PromotionWebhookUrl { get; set; }
        public string? PromotionWebhookSecret { get; set; }
    }

    public class CreateSiteReq : SiteReq
    {
    }

    public class UpdateSiteReq : SiteReq
    {
        // Note: Id is not included here because it comes from the route parameter, not the request body
        // This ensures updates are always by ID from the route, not from the request body
    }
}
