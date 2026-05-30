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
        /// <summary>OC Wolt Drive dispatch API bearer token (WP admin → Wolt Drive → Webhook).</summary>
        public string? WoltDispatchToken { get; set; }
        /// <summary>When true, Wolt courier features are enabled for this site.</summary>
        public bool? WoltEnabled { get; set; }
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

        /// <summary>When true (default), UI asks for bag count when completing picking.</summary>
        public bool? AskBagsCountAtPickingFinish { get; set; }

        public string? PaymentGatewayProvider { get; set; }
        public int? CardcomTerminalNumber { get; set; }
        public string? CardcomApiName { get; set; }
        public bool? HasCardcomApiPassword { get; set; }
        public bool? CardcomSaveCardEnabled { get; set; }
        public int? PaymentAuthBufferPercent { get; set; }
        public decimal? PaymentMaxAuthAmount { get; set; }
        public bool? PaymentAllowCaptureAboveAuth { get; set; }
        public string? CardcomCssUrl { get; set; }
        public string? CardcomLogoUrl { get; set; }

        /// <summary>When the site's account has kiosk enabled, contains the account kiosk settings (including showOutOfStockProducts, showOutOfStockAtBottom).</summary>
        public KioskSettingsRes? KioskSettings { get; set; }
    }
}
