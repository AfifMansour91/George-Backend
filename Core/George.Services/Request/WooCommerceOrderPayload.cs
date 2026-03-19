using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace George.Services.Request;

/// <summary>Payload from WooCommerce plugin when order is opened/edited. Matches their JSON (camelCase).</summary>
public class WooCommerceOrderPayload
{
    [Required]
    [JsonPropertyName("orderNumber")]
    public string OrderNumber { get; set; } = null!;

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

public class WooCommerceOrderItemPayload
{
    [JsonPropertyName("productId")]
    public int? ProductId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("sku")]
    public string? Sku { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unitPrice")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("lineTotal")]
    public decimal? LineTotal { get; set; }
}
