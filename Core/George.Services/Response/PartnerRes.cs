namespace George.Services.Response;

// Partner API (/Partner/v1) response DTOs. Deliberately narrower than the internal DTOs:
// no cost prices, supplier, account internals or CRM manager notes are ever exposed to partners.
// Contract for integrators: docs/PARTNER_API.md.

/// <summary>Store facts an ordering agent needs before it can talk to a customer (GET /Partner/v1/Site).</summary>
public class PartnerSiteRes
{
    public int SiteId { get; set; }
    public string? SiteName { get; set; }
    /// <summary>Business (account) name - the brand the customer knows.</summary>
    public string? BusinessName { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    /// <summary>Pickup address (branch location).</summary>
    public string? Address { get; set; }
    public string? City { get; set; }
    public string Currency { get; set; } = "ILS";
    public bool IsKosher { get; set; }
    /// <summary>Pickup (איסוף עצמי) orders accepted.</summary>
    public bool PickupEnabled { get; set; } = true;
    /// <summary>Home delivery (משלוח) orders accepted.</summary>
    public bool ShippingEnabled { get; set; } = true;
    /// <summary>Default delivery fee when the delivery city has no specific fee.</summary>
    public decimal ShippingCost { get; set; }
    /// <summary>Delivery is free when the order subtotal (before discounts) reaches this amount. Null = never free.</summary>
    public decimal? FreeShippingAbove { get; set; }
    /// <summary>Per-city delivery fees (override <see cref="ShippingCost"/>). A city not listed uses the default fee.</summary>
    public List<PartnerCityShippingRes> ShippingCities { get; set; } = new();
    /// <summary>Typical preparation time in minutes (null = not configured).</summary>
    public int? PrepTimeMinutes { get; set; }
    /// <summary>none | cardcom | payplus. When "none", only non-card payment methods are available.</summary>
    public string PaymentGateway { get; set; } = "none";
    /// <summary>Payment methods this site accepts on partner orders (codes for PartnerCreateOrderReq.PaymentMethod).</summary>
    public List<PartnerPaymentMethodRes> PaymentMethods { get; set; } = new();
    /// <summary>Always true on partner orders: PickupDate / DeliveryDate must be sent (see GET Availability).</summary>
    public bool SupplyDateRequired { get; set; } = true;
    /// <summary>Days on which the shop does not accept orders, per delivery type.</summary>
    public PartnerOrderReceptionRes OrderReception { get; set; } = new();
    /// <summary>True when an order-events webhook URL is configured for this site.</summary>
    public bool WebhookConfigured { get; set; }
    /// <summary>Bundle products (מארזים) are not orderable through the Partner API (v1) and are omitted from the catalog.</summary>
    public bool BundlesSupported { get; set; }
    /// <summary>Server time zone used for dates (IANA id).</summary>
    public string TimeZone { get; set; } = "Asia/Jerusalem";
}

public class PartnerCityShippingRes
{
    public string City { get; set; } = null!;
    public decimal Cost { get; set; }
}

public class PartnerPaymentMethodRes
{
    /// <summary>Cash | PaymentLink | SavedCard | OnAccount | BankTransfer.</summary>
    public string Code { get; set; } = null!;
    /// <summary>Hebrew display name.</summary>
    public string Name { get; set; } = null!;
    /// <summary>Hint for the agent (e.g. "call POST Orders/{id}/PaymentLink after creating the order").</summary>
    public string? Description { get; set; }
}

public class PartnerOrderReceptionRes
{
    public bool TodayDeliveryClosed { get; set; }
    public bool TodayPickupClosed { get; set; }
    /// <summary>yyyy-MM-dd dates (today or later) on which delivery orders are not accepted.</summary>
    public List<string> ClosedDeliveryDates { get; set; } = new();
    /// <summary>yyyy-MM-dd dates (today or later) on which pickup orders are not accepted.</summary>
    public List<string> ClosedPickupDates { get; set; } = new();
}

/// <summary>GET /Partner/v1/Availability - which supply dates can be offered to the customer.</summary>
public class PartnerAvailabilityRes
{
    /// <summary>Pickup | Shipping.</summary>
    public string DeliveryType { get; set; } = null!;
    /// <summary>Today in the shop's time zone (yyyy-MM-dd).</summary>
    public string Today { get; set; } = null!;
    public List<PartnerAvailabilityDayRes> Days { get; set; } = new();
    /// <summary>
    /// Suggested time windows to offer (free text - the shop does not enforce slots). Send the chosen window as
    /// PickupTime / DeliveryTime on the order, or any free text the customer said ("אחרי 17:00").
    /// </summary>
    public List<string> SuggestedTimeWindows { get; set; } = new();
}

public class PartnerAvailabilityDayRes
{
    /// <summary>yyyy-MM-dd.</summary>
    public string Date { get; set; } = null!;
    /// <summary>Sunday … Saturday (English).</summary>
    public string DayOfWeek { get; set; } = null!;
    /// <summary>Hebrew day name (ראשון … שבת).</summary>
    public string DayName { get; set; } = null!;
    public bool Available { get; set; }
    /// <summary>Why the day is unavailable (Hebrew) - null when available.</summary>
    public string? Reason { get; set; }
}

public class PartnerProductRes
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public string? ShortDescription { get; set; }
    public string? Sku { get; set; }
    /// <summary>Regular unit price at this site (per kg for weight-sold products).</summary>
    public decimal? Price { get; set; }
    /// <summary>Sale price when a sale window is active right now, else null.</summary>
    public decimal? SalePrice { get; set; }
    /// <summary>The price to charge right now (active sale price, else regular price).</summary>
    public decimal? EffectivePrice { get; set; }
    /// <summary>unit | weight - how Quantity is interpreted on order lines (weight = kg).</summary>
    public string SoldBy { get; set; } = "unit";
    /// <summary>kg | g | ml (weight-sold products).</summary>
    public string? WeightUnit { get; set; }
    /// <summary>Approximate weight of one unit in grams for unit-sold products that are weighed (e.g. a whole fish). Null when unknown.</summary>
    public decimal? ApproxUnitWeightGrams { get; set; }
    public bool InStock { get; set; }
    /// <summary>True when the product has variants: order lines must carry a ProductVariantId.</summary>
    public bool RequiresVariant { get; set; }
    public List<int> CategoryIds { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string? ImageUrl { get; set; }
    public bool? IsKosher { get; set; }
    public List<PartnerProductOptionRes> Options { get; set; } = new();
    public List<PartnerProductVariantRes> Variants { get; set; } = new();
}

public class PartnerProductOptionRes
{
    public string? Name { get; set; }
    public List<string> Values { get; set; } = new();
}

public class PartnerProductVariantRes
{
    public int Id { get; set; }
    /// <summary>Human-readable variant title built from its option values (e.g. "שלם / מנוקה").</summary>
    public string? Title { get; set; }
    public Dictionary<string, string>? OptionValues { get; set; }
    public decimal? Price { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? EffectivePrice { get; set; }
    public string? Sku { get; set; }
    public bool InStock { get; set; }
}

public class PartnerCategoryRes
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? Description { get; set; }
    public int? SortOrder { get; set; }
    public string? ImageUrl { get; set; }
}

public class PartnerCustomerRes
{
    public bool Found { get; set; }
    public int? CustomerId { get; set; }
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? City { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
    public string? DefaultAddress { get; set; }
    public int OrderCount { get; set; }
    public DateTime? LastOrderDate { get; set; }
    /// <summary>The customer has a card saved at this site's payment gateway - PaymentMethod "SavedCard" is possible.</summary>
    public bool HasSavedCard { get; set; }
    public string? SavedCardLast4 { get; set; }
    public string? SavedCardBrand { get; set; }
    /// <summary>Customer consented to marketing SMS.</summary>
    public bool MarketingSms { get; set; }
}

/// <summary>POST /Partner/v1/Orders/Quote - server-side pricing of a cart before the customer confirms.</summary>
public class PartnerQuoteRes
{
    /// <summary>False when any line could not be priced; see <see cref="Errors"/>. An invalid cart cannot be ordered.</summary>
    public bool IsValid { get; set; }
    public List<PartnerQuoteErrorRes> Errors { get; set; } = new();
    public List<PartnerQuoteLineRes> Lines { get; set; } = new();
    /// <summary>Sum of line totals before discounts.</summary>
    public decimal SubTotal { get; set; }
    /// <summary>Total promotion / coupon discount.</summary>
    public decimal DiscountTotal { get; set; }
    public decimal ShippingCost { get; set; }
    public bool FreeShippingApplied { get; set; }
    /// <summary>SubTotal − DiscountTotal + ShippingCost. Weight-sold lines are re-priced at picking by actual weight.</summary>
    public decimal Total { get; set; }
    /// <summary>True when any line is sold by weight - the final charge may differ from <see cref="Total"/>.</summary>
    public bool HasWeightLines { get; set; }
    public string? CouponCode { get; set; }
    public bool CouponApplied { get; set; }
    /// <summary>Hebrew message when a coupon was sent but did not apply.</summary>
    public string? CouponMessage { get; set; }
    public List<PartnerAppliedPromotionRes> PromotionsApplied { get; set; } = new();
    /// <summary>Promotions the cart almost qualifies for ("add X more to get …").</summary>
    public List<PartnerNearbyPromotionRes> PromotionsNearby { get; set; } = new();
}

public class PartnerQuoteErrorRes
{
    /// <summary>1-based line index, null for cart-level errors.</summary>
    public int? Line { get; set; }
    /// <summary>Machine code, see docs/PARTNER_API.md "Error codes".</summary>
    public string Code { get; set; } = null!;
    public string Message { get; set; } = null!;
}

public class PartnerQuoteLineRes
{
    public int Line { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? Title { get; set; }
    public string? VariantTitle { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>unit | weight.</summary>
    public string SoldBy { get; set; } = "unit";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? PromotionName { get; set; }
    public string? Notes { get; set; }
}

public class PartnerAppliedPromotionRes
{
    public int PromotionId { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class PartnerNearbyPromotionRes
{
    public int PromotionId { get; set; }
    public string? Name { get; set; }
    public decimal? PotentialSaving { get; set; }
}

public class PartnerOrderRes
{
    public int OrderId { get; set; }
    public string? OrderNumber { get; set; }
    /// <summary>Echo of the partner's idempotency reference (PartnerCreateOrderReq.PartnerRef).</summary>
    public string? PartnerRef { get; set; }
    /// <summary>True when this request matched an order already created with the same PartnerRef (idempotent replay).</summary>
    public bool AlreadyExisted { get; set; }
    public string? Source { get; set; }
    /// <summary>New | InTreatment | Ready | Completed | Cancelled.</summary>
    public string? Status { get; set; }
    /// <summary>Unpaid | Paid | Refunded.</summary>
    public string? PaymentStatus { get; set; }
    /// <summary>Cash | CreditSms (payment link) | SavedCard | OnAccount | BankTransfer | ExternalCredit | WooCommerce …</summary>
    public string? PaymentMethod { get; set; }
    /// <summary>None | Initiated | Authorized | Captured | Voided … (card orders only).</summary>
    public string? PaymentSettleStatus { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Invoice / receipt document URL once the charge was captured (null before).</summary>
    public string? InvoiceUrl { get; set; }
    public string? DeliveryType { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTime { get; set; }
    public DateTime? PickupDate { get; set; }
    public string? PickupTime { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
    /// <summary>Courier tracking link when the order was handed to a delivery provider.</summary>
    public string? DeliveryTrackingLink { get; set; }
    /// <summary>Delivery provider status (provider vocabulary) when dispatched.</summary>
    public string? DeliveryStatus { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerNote { get; set; }
    public string? CouponCode { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? Total { get; set; }
    public int? BagsCount { get; set; }
    /// <summary>True while the partner may still cancel (status New).</summary>
    public bool CanCancel { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? InTreatmentAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public List<PartnerOrderItemRes> Items { get; set; } = new();
}

public class PartnerOrderItemRes
{
    public int OrderItemId { get; set; }
    public int? ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? Sku { get; set; }
    public string? Title { get; set; }
    public string? VariantTitle { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Quantity / weight actually picked (null until picking).</summary>
    public decimal? PickedQuantity { get; set; }
    public decimal? PricePerUnit { get; set; }
    public decimal? TotalPrice { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
}

public class PartnerPaymentLinkRes
{
    public int OrderId { get; set; }
    public string? PaymentUrl { get; set; }
    /// <summary>True when no link was created because the order is already authorized / paid.</summary>
    public bool AlreadyPaid { get; set; }
}

/// <summary>Admin view of a site's Partner API configuration (secrets are never returned).</summary>
public class PartnerSettingsRes
{
    public int SiteId { get; set; }
    public string? SiteName { get; set; }
    public bool HasApiKey { get; set; }
    /// <summary>First characters of the key so an admin can tell which key is installed.</summary>
    public string? ApiKeyPrefix { get; set; }
    public string? WebhookUrl { get; set; }
    public bool HasWebhookSecret { get; set; }
}

public class PartnerWebhookTestRes
{
    public bool Sent { get; set; }
    public int? HttpStatus { get; set; }
    public string? Error { get; set; }
    public int DurationMs { get; set; }
}
