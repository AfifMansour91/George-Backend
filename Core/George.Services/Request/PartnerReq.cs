using System.ComponentModel.DataAnnotations;

namespace George.Services.Request;

/// <summary>
/// Partner API (/Partner/v1) order creation. Site is resolved from the API key, never from the body.
/// Lines reference catalog products by George ProductId; unit prices are always computed server-side
/// from the site's effective catalog - client-supplied prices are ignored by design (the ordering
/// agent must not be able to invent prices). Contract: docs/PARTNER_API.md.
/// </summary>
public class PartnerCreateOrderReq
{
    /// <summary>Partner's own idempotency reference (e.g. WhatsApp conversation/order id). Stored as ExternalOrderId; resubmitting the same ref returns the existing order instead of creating a duplicate.</summary>
    [StringLength(100)]
    public string? PartnerRef { get; set; }

    /// <summary>Order source label. Allowed: WhatsApp (default), Partner.</summary>
    public string? Source { get; set; }

    [Required]
    public string CustomerName { get; set; } = null!;

    [Required]
    public string CustomerPhone { get; set; } = null!;

    public string? CustomerEmail { get; set; }

    /// <summary>Customer consented to marketing SMS (persisted on the CRM customer). Null = leave unchanged.</summary>
    public bool? MarketingSms { get; set; }

    /// <summary>Pickup | Shipping.</summary>
    [Required]
    public string DeliveryType { get; set; } = null!;

    /// <summary>Required when DeliveryType is Pickup. Calendar date (yyyy-MM-dd).</summary>
    public DateOnly? PickupDate { get; set; }
    /// <summary>Free text time window for pickup (e.g. "12:00-14:00").</summary>
    public string? PickupTime { get; set; }

    /// <summary>Required when DeliveryType is Shipping. Calendar date (yyyy-MM-dd).</summary>
    public DateOnly? DeliveryDate { get; set; }
    /// <summary>Free text time window for delivery (e.g. "16:00-19:00").</summary>
    public string? DeliveryTime { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }

    /// <summary>
    /// Cash (default) | PaymentLink | SavedCard | OnAccount | BankTransfer - must be one of the site's enabled
    /// methods (GET /Partner/v1/Site → PaymentMethods). PaymentLink: create the order, then call
    /// POST Orders/{id}/PaymentLink and send the URL to the customer. SavedCard: the customer's saved card is
    /// held/charged automatically (requires PartnerCustomerRes.HasSavedCard).
    /// </summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Coupon code the customer gave (validated server-side; an unknown coupon does not fail the order - check the response DiscountTotal).</summary>
    [StringLength(50)]
    public string? CouponCode { get; set; }

    /// <summary>Free-text note from the end customer (shown on the order).</summary>
    public string? CustomerNote { get; set; }

    public List<PartnerOrderItemReq> Items { get; set; } = new();
}

public class PartnerOrderItemReq
{
    /// <summary>George catalog product id (from /Partner/v1/Products). Required - generic non-catalog lines are not allowed on partner orders.</summary>
    [Required]
    public int ProductId { get; set; }

    /// <summary>Variant id when the product has variants (from PartnerProductRes.Variants).</summary>
    public int? ProductVariantId { get; set; }

    /// <summary>Quantity in the product's selling unit: kg for weight-sold products, units otherwise.</summary>
    [Required]
    public decimal Quantity { get; set; }

    /// <summary>Per-line preparation note (e.g. "מנוקה ומפולט", "חתוך לקוביות").</summary>
    public string? Notes { get; set; }
}

/// <summary>POST /Partner/v1/Orders/Quote - price a cart exactly as an order would be priced, without creating anything.</summary>
public class PartnerQuoteReq
{
    [Required]
    public List<PartnerOrderItemReq> Items { get; set; } = new();

    /// <summary>Pickup | Shipping (default Pickup). Shipping adds the delivery fee.</summary>
    public string? DeliveryType { get; set; }

    /// <summary>Delivery city - selects the per-city delivery fee.</summary>
    public string? DeliveryCity { get; set; }

    public string? CouponCode { get; set; }

    /// <summary>Customer phone - enables per-customer promotion limits (optional).</summary>
    public string? CustomerPhone { get; set; }

    /// <summary>Optional: validated against the shop's closed dates when sent.</summary>
    public DateOnly? PickupDate { get; set; }
    public DateOnly? DeliveryDate { get; set; }
}

/// <summary>Admin: configure the outbound order-events webhook of a site.</summary>
public class PartnerWebhookReq
{
    [Required]
    public int SiteId { get; set; }

    /// <summary>Absolute https URL. Null/empty removes the webhook.</summary>
    [StringLength(500)]
    public string? Url { get; set; }

    /// <summary>HMAC secret. Null keeps the current secret; empty string clears it.</summary>
    [StringLength(200)]
    public string? Secret { get; set; }
}
