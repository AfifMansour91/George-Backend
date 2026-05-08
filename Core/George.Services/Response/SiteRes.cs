namespace George.Services.Response
{
    public class SiteRes
    {
        public int Id { get; set; }
        public DateTime CreationTime { get; set; }
        public DateTime? UpdatedDate { get; set; }
        public int? CreationUserId { get; set; }
        public int AccountId { get; set; }
        public string SiteName { get; set; } = null!;
        public string? Location { get; set; }
        public string? Description { get; set; }
        public List<int> BusinessTypeIds { get; set; } = new();
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
        public bool? VoucherPrinterSilent { get; set; }
        public string? VoucherPrinterName { get; set; }

        // Promotion settings (Sprint 4)
        /// <summary>"same_price" | "full_price" (default). Default for BxPY over-quantity pricing.</summary>
        public string? PromotionOveragePolicyDefault { get; set; }
        public bool? PromotionsApplyToPhoneOrders { get; set; }
        public bool? PromotionsApplyToDiscountedProducts { get; set; }
        /// <summary>URL the storefront/kiosk listens on for promotion lifecycle events.</summary>
        public string? PromotionWebhookUrl { get; set; }
        public string? PromotionWebhookSecret { get; set; }

        /// <summary>When the site's account has kiosk enabled, contains the account kiosk settings (including showOutOfStockProducts, showOutOfStockAtBottom).</summary>
        public KioskSettingsRes? KioskSettings { get; set; }
    }
}
