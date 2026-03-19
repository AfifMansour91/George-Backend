using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace George.Services.Request;

/// <summary>Payload from WooCommerce plugin when order is opened/edited. Matches their JSON (camelCase and snake_case for new fields).</summary>
public class WooCommerceOrderPayload
{
    [Required]
    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = null!;

    /// <summary>WooCommerce order ID; may be sent as number. Used as external reference (we also use orderNumber).</summary>
    [JsonPropertyName("externalOrderId")]
    public object? ExternalOrderId { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = "WooCommerce";

    /// <summary>Optional external site identifier; API key already identifies our SiteId.</summary>
    [JsonPropertyName("siteId")]
    public string? SiteId { get; set; }

    /// <summary>on-hold (ordered), completed, cancelled</summary>
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("orderDate")]
    public DateTime? OrderDate { get; set; }

    [JsonPropertyName("customer")]
    public WooCommerceCustomerPayload? Customer { get; set; }

    [JsonPropertyName("shippingAddress")]
    public WooCommerceShippingAddressPayload? ShippingAddress { get; set; }

    [JsonPropertyName("items")]
    public List<WooCommerceOrderItemPayload> Items { get; set; } = new();

    [JsonPropertyName("shippingTotal")]
    public decimal? ShippingTotal { get; set; }

    [JsonPropertyName("orderTotal")]
    public decimal? OrderTotal { get; set; }

    [JsonPropertyName("customerNotes")]
    public string? CustomerNotes { get; set; }

    /// <summary>Shipping method label (e.g. "איסוף עצמי").</summary>
    [JsonPropertyName("shipping_label")]
    public string? ShippingLabel { get; set; }

    /// <summary>Payment method label (e.g. "תשלום לשליח").</summary>
    [JsonPropertyName("payment_label")]
    public string? PaymentLabel { get; set; }

    /// <summary>Delivery/pickup slot and type (type, date DD/MM/YYYY, slotStart, slotEnd, pickupAffiliateId, pickupAffiliateName).</summary>
    [JsonPropertyName("shippingInfo")]
    public WooCommerceShippingInfoPayload? ShippingInfo { get; set; }
}

public class WooCommerceShippingInfoPayload
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("slotStart")]
    public string? SlotStart { get; set; }

    [JsonPropertyName("slotEnd")]
    public string? SlotEnd { get; set; }

    [JsonPropertyName("pickupAffiliateId")]
    public string? PickupAffiliateId { get; set; }

    [JsonPropertyName("pickupAffiliateName")]
    public string? PickupAffiliateName { get; set; }
}

public class WooCommerceCustomerPayload
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public class WooCommerceShippingAddressPayload
{
    [JsonPropertyName("street")]
    public string? Street { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("zip")]
    public string? Zip { get; set; }
}

/// <summary>One selected option in a variant (e.g. cutting shape or size). Id = WooCommerce attribute/value id, Name = display text.</summary>
public class WooCommerceVariantOptionPayload
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public class WooCommerceOrderItemPayload
{
    [JsonPropertyName("productId")]
    public int? ProductId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    /// <summary>WooCommerce variation ID (optional). For variable products.</summary>
    [JsonPropertyName("variationId")]
    public int? VariationId { get; set; }

    /// <summary>Array of selected variant options. Each has id (WooCommerce option/value id) and name (display text). For display we join names with " | ".</summary>
    [JsonPropertyName("variants")]
    public List<WooCommerceVariantOptionPayload>? Variants { get; set; }

    /// <summary>Customer note for this line item (הערת לקוח לפריט).</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("lineTotal")]
    public decimal? LineTotal { get; set; }
}
