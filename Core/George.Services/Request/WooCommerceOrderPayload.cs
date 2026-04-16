using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace George.Services.Request;

/// <summary>Payload from WooCommerce plugin when order is opened/edited. Matches their JSON (camelCase and snake_case for new fields).</summary>
/// <remarks>
/// API uses <c>AddNewtonsoftJson</c> with camelCase contract. <see cref="JsonPropertyNameAttribute"/> (System.Text.Json) is ignored on deserialize;
/// snake_case keys must use <see cref="JsonPropertyAttribute"/> (Newtonsoft).
/// </remarks>
public class WooCommerceOrderPayload
{
    [Required]
    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = null!;

    /// <summary>WooCommerce order ID; may be sent as number. Used as external reference (we also use orderNumber).</summary>
    [JsonPropertyName("externalOrderId")]
    public object? ExternalOrderId { get; set; }

    /// <summary>Echo from plugin; not read when creating orders (we always set Source = WooCommerce).</summary>
    [JsonPropertyName("source")]
    public string Source { get; set; } = "WooCommerce";

    /// <summary>Optional external site id string; site is resolved from API key. Only used if you re-enable anonymous + SiteId fallback in the controller.</summary>
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

    /// <summary>Cashier / checkout notes from WooCommerce (camelCase JSON).</summary>
    [JsonPropertyName("billing_notes")]
    public string? BillingNotes { get; set; }

    /// <summary>Same as <see cref="BillingNotes"/>; some plugins send camelCase only.</summary>
    [JsonPropertyName("billingNotes")]
    public string? BillingNotesCamel { get; set; }

    /// <summary>Snake_case <c>billing_notes</c> (Newtonsoft binding).</summary>
    [JsonProperty("billing_notes")]
    public string? BillingNotesSnake { get; set; }

    /// <summary>First non-empty of snake_case or camelCase billing note.</summary>
    public string? GetResolvedBillingNotes()
    {
        var snake = BillingNotesSnake?.Trim();
        if (!string.IsNullOrWhiteSpace(snake)) return snake;
        var a = BillingNotes?.Trim();
        if (!string.IsNullOrWhiteSpace(a)) return a;
        var b = BillingNotesCamel?.Trim();
        return string.IsNullOrWhiteSpace(b) ? null : b;
    }

    /// <summary>Internal WooCommerce / plugin order notes (status history, sync messages).</summary>
    [JsonPropertyName("internalOrderNotes")]
    public string? InternalOrderNotes { get; set; }

    /// <summary>WooCommerce payment method code (e.g. cod, bacs).</summary>
    [JsonPropertyName("paymentMethod")]
    public string? PaymentMethod { get; set; }

    /// <summary>Human-readable payment method title.</summary>
    [JsonPropertyName("paymentMethodTitle")]
    public string? PaymentMethodTitle { get; set; }

    /// <summary>Shipping method label (e.g. "איסוף עצמי"); camelCase JSON <c>shippingLabel</c>.</summary>
    [JsonPropertyName("shipping_label")]
    public string? ShippingLabel { get; set; }

    /// <summary>Snake_case <c>shipping_label</c> (Newtonsoft binding).</summary>
    [JsonProperty("shipping_label")]
    public string? ShippingLabelSnake { get; set; }

    /// <summary>Same as <see cref="ShippingLabel"/>; plugin may send one word key <c>shippinglabel</c>.</summary>
    [JsonPropertyName("shippinglabel")]
    [JsonProperty("shippinglabel")]
    public string? ShippingLabelFlat { get; set; }

    /// <summary>Payment method label (e.g. "תשלום לשליח"); camelCase JSON <c>paymentLabel</c>.</summary>
    [JsonPropertyName("payment_label")]
    public string? PaymentLabel { get; set; }

    /// <summary>Snake_case <c>payment_label</c> (Newtonsoft binding).</summary>
    [JsonProperty("payment_label")]
    public string? PaymentLabelSnake { get; set; }

    /// <summary>Same as <see cref="PaymentLabel"/>; plugin may send <c>paymentlabel</c>.</summary>
    [JsonPropertyName("paymentlabel")]
    [JsonProperty("paymentlabel")]
    public string? PaymentLabelFlat { get; set; }

    /// <summary>Pickup branch name (e.g. "סניף חיפה"); camelCase <c>shippingStoreName</c>.</summary>
    [JsonPropertyName("shippingstorename")]
    public string? ShippingStoreName { get; set; }

    /// <summary>All-lowercase <c>shippingstorename</c> from plugin (Newtonsoft binding).</summary>
    [JsonProperty("shippingstorename")]
    public string? ShippingStoreNameSnake { get; set; }

    /// <summary>Delivery/pickup slot and type (type, date DD/MM/YYYY, slotStart, slotEnd, pickupAffiliateId, pickupAffiliateName).</summary>
    [JsonPropertyName("shippingInfo")]
    public WooCommerceShippingInfoPayload? ShippingInfo { get; set; }

    public string? GetResolvedShippingLabel()
    {
        var a = ShippingLabel?.Trim();
        if (!string.IsNullOrWhiteSpace(a)) return a;
        var snake = ShippingLabelSnake?.Trim();
        if (!string.IsNullOrWhiteSpace(snake)) return snake;
        var b = ShippingLabelFlat?.Trim();
        return string.IsNullOrWhiteSpace(b) ? null : b;
    }

    public string? GetResolvedPaymentLabel()
    {
        var a = PaymentLabel?.Trim();
        if (!string.IsNullOrWhiteSpace(a)) return a;
        var snake = PaymentLabelSnake?.Trim();
        if (!string.IsNullOrWhiteSpace(snake)) return snake;
        var b = PaymentLabelFlat?.Trim();
        return string.IsNullOrWhiteSpace(b) ? null : b;
    }

    /// <summary>Branch name from camelCase or all-lowercase plugin key.</summary>
    public string? GetResolvedShippingStoreName()
    {
        var a = ShippingStoreName?.Trim();
        if (!string.IsNullOrWhiteSpace(a)) return a;
        var b = ShippingStoreNameSnake?.Trim();
        return string.IsNullOrWhiteSpace(b) ? null : b;
    }
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

    [JsonPropertyName("apartment")]
    public string? Apartment { get; set; }

    [JsonPropertyName("floor")]
    public string? Floor { get; set; }

    [JsonPropertyName("entranceCode")]
    public string? EntranceCode { get; set; }

    /// <summary>Plugin alias for <see cref="EntranceCode"/>.</summary>
    [JsonPropertyName("enterCode")]
    [JsonProperty("enterCode")]
    public string? EnterCode { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? ResolvedEntranceCode =>
        string.IsNullOrWhiteSpace(EntranceCode) ? (string.IsNullOrWhiteSpace(EnterCode) ? null : EnterCode.Trim()) : EntranceCode.Trim();
}

/// <summary>One selected option in a variant (e.g. cutting shape or size). <see cref="Name"/> is used for display; <see cref="Id"/> is accepted from JSON but not used for matching (we use variationId / names).</summary>
public class WooCommerceVariantOptionPayload
{
    [JsonPropertyName("id")]
    public int? Id { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

/// <summary>Alternative variant format from plugin: variationId + attributes/meta (option name → value). We map this to VariationId and variant title.</summary>
public class WooCommerceVariationPayload
{
    [JsonPropertyName("variationId")]
    public int? VariationId { get; set; }

    [JsonPropertyName("attributes")]
    public Dictionary<string, string?>? Attributes { get; set; }

    [JsonPropertyName("meta")]
    public Dictionary<string, string?>? Meta { get; set; }
}

public class WooCommerceOrderItemPayload
{
    [JsonPropertyName("productId")]
    public int? ProductId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    /// <summary>WooCommerce variation ID (optional). For variable products. Also set from variation.variationId when plugin sends variation object.</summary>
    [JsonPropertyName("variationId")]
    public int? VariationId { get; set; }

    /// <summary>Array of selected variant options (id + name). Plugin may instead send variation (object with variationId, attributes, meta).</summary>
    [JsonPropertyName("variants")]
    public List<WooCommerceVariantOptionPayload>? Variants { get; set; }

    /// <summary>Variant as object (plugin format). If present we use variationId and attributes/meta for display title.</summary>
    [JsonPropertyName("variation")]
    public WooCommerceVariationPayload? Variation { get; set; }

    /// <summary>Customer note for this line item. We also accept productNote from the plugin.</summary>
    [JsonPropertyName("note")]
    public string? Note { get; set; }

    [JsonPropertyName("productNote")]
    public string? ProductNote { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    /// <summary><c>unit</c> or <c>kg</c> from WooCommerce plugin.</summary>
    [JsonPropertyName("quantityType")]
    public string? QuantityType { get; set; }

    /// <summary>When <see cref="QuantityType"/> is <c>kg</c>: number of pieces (e.g. 3). Optional for <c>unit</c>.</summary>
    [JsonPropertyName("unit")]
    public decimal? Unit { get; set; }

    /// <summary>When <see cref="QuantityType"/> is <c>kg</c>: weight per piece in kg (e.g. 0.6).</summary>
    [JsonPropertyName("unitWeight")]
    public decimal? UnitWeight { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("lineTotal")]
    public decimal? LineTotal { get; set; }

    /// <summary>Display string for units sold (e.g. "1 יח'"). Parsed for quantity when present.</summary>
    [JsonPropertyName("saleUnits")]
    public string? SaleUnits { get; set; }

    /// <summary>Total ordered weight as string (typically kg, e.g. "0.8 "). Parsed when present.</summary>
    [JsonPropertyName("saleTotalWeight")]
    public string? SaleTotalWeight { get; set; }

    /// <summary>Combined display e.g. "3 יח', 600 גר'" (optional).</summary>
    [JsonPropertyName("saleUnitsLine")]
    public string? SaleUnitsLine { get; set; }
}
