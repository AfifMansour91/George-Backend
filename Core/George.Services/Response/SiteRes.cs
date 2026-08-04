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
        /// <summary>Customer-behavior: single-order customer is inactive / at-risk after this many days without an order (default 14).</summary>
        public int? CustomerInactiveAfterDays { get; set; }
        /// <summary>Customer-behavior: include not-yet-completed orders in customer statistics (default true).</summary>
        public bool? IncludeIncompleteOrdersInStats { get; set; }
        public decimal? ShippingCost { get; set; }
        public decimal? FreeShippingAbove { get; set; }
        /// <summary>When true, manual/phone order uses searchable Israel city picker (default true).</summary>
        public bool? IsraelCityPickerEnabled { get; set; }
        /// <summary>Manual-order delivery: open a confirm popup (city + editable fee) when choosing home delivery (default off).</summary>
        public bool? ConfirmDeliveryFeePopup { get; set; }
        public bool? AutoPrintEnabled { get; set; }
        /// <summary>Print order printouts on A4 pages instead of the thermal voucher.</summary>
        public bool? VoucherPrintA4 { get; set; }
        /// <summary>Omit the delivery/pickup time from order printouts (date only).</summary>
        public bool? VoucherHideDeliveryTime { get; set; }
        /// <summary>Customer sticker uses the wide 120mm pre-printed branded label layout (default off).</summary>
        public bool? CustomerLabelWideFormat { get; set; }
        public bool? PrintNewOrderImmediate { get; set; }
        public bool? PrintMovedToTreatment { get; set; }
        public bool? PrintAfterPicking { get; set; }
        public bool? PrintFutureImmediate { get; set; }
        public bool? PrintFutureAtTimeEnabled { get; set; }
        public string? PrintFutureAtTime { get; set; }
        public bool? VoucherPrinterSilent { get; set; }
        public string? VoucherPrinterName { get; set; }
        public bool? VoucherPrinterUseAgent { get; set; }
        public string? LabelPrinterName { get; set; }
        public bool? LabelPrinterUseAgent { get; set; }
        public bool? PrintLabelsAfterPicking { get; set; }

        // Promotion settings (Sprint 4)
        /// <summary>"same_price" | "full_price" (default). Default for BxPY over-quantity pricing.</summary>
        public string? PromotionOveragePolicyDefault { get; set; }
        public bool? PromotionsApplyToPhoneOrders { get; set; }
        public bool? PromotionsApplyToDiscountedProducts { get; set; }
        /// <summary>URL the storefront/kiosk listens on for promotion lifecycle events.</summary>
        public string? PromotionWebhookUrl { get; set; }
        public string? PromotionWebhookSecret { get; set; }

        /// <summary>When true (default), UI asks for bag count when completing picking.</summary>
        public bool? AskBagsCountAtPickingFinish { get; set; }

        /// <summary>When true (default), UI shows picking confirmation dialog after barcode scan.</summary>
        public bool? ConfirmPickingAfterScan { get; set; }

        /// <summary>When true (default), show picking deviation in footer and archive.</summary>
        public bool? ShowPickingDeviation { get; set; }

        /// <summary>When true, show the product SKU (מק"ט) on picking-screen lines. Off by default.</summary>
        public bool? ShowSkuInPicking { get; set; }

        /// <summary>When true, hide per-unit / size approximate weight on printouts, order cards and picking. Off by default.</summary>
        public bool? HideUnitWeightInOrders { get; set; }

        /// <summary>When true, printouts include the permanent customer note (הערה קבועה). Off by default.</summary>
        public bool? ShowCustomerProfileNoteInPrints { get; set; }

        /// <summary>Feature flag: render order lines from the typed LineDisplayJson snapshot when present. Off by default.</summary>
        public bool? UseStructuredOrderLineDisplay { get; set; }

        /// <summary>When true, fast-scan picking: no confirm dialog on scan + auto-finish when all items picked. Off by default.</summary>
        public bool? FastPickingScan { get; set; }

        /// <summary>When true (default), show the deviation (חריגה) popup when a scan exceeds tolerance.</summary>
        public bool? ShowPickingExceptionsPopup { get; set; }

        /// <summary>When true, this branch uses a connected RS232 scale (live weight in picking). Off by default.</summary>
        public bool? ScaleEnabled { get; set; }

        /// <summary>Scale barcode payload: auto | weight | price.</summary>
        public string? ScaleBarcodeEmbedMode { get; set; }

        /// <summary>When true, prices on this site's Woo store are managed externally (POS): George skips price fields on Woo updates and the daily job pulls Woo prices back. Off by default.</summary>
        public bool? ExternalPriceManagement { get; set; }

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
