using System.ComponentModel.DataAnnotations;

namespace George.Services.Request;

/// <summary>Payload from WooCommerce plugin when order is opened/edited. Matches their JSON.</summary>
public class WooCommerceOrderPayload
{
    [Required]
    public string OrderNumber { get; set; } = null!;
    public string Source { get; set; } = "WooCommerce";
    /// <summary>Optional external site identifier; API key already identifies our SiteId.</summary>
    public string? SiteId { get; set; }
    /// <summary>on-hold (ordered), completed, cancelled</summary>
    public string? Status { get; set; }
    public DateTime? OrderDate { get; set; }
    public WooCommerceCustomerPayload? Customer { get; set; }
    public WooCommerceShippingAddressPayload? ShippingAddress { get; set; }
    public List<WooCommerceOrderItemPayload> Items { get; set; } = new();
    public decimal? ShippingTotal { get; set; }
    public decimal? OrderTotal { get; set; }
    public string? CustomerNotes { get; set; }
}

public class WooCommerceCustomerPayload
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class WooCommerceShippingAddressPayload
{
    public string? Street { get; set; }
    public string? City { get; set; }
    public string? Zip { get; set; }
}

public class WooCommerceOrderItemPayload
{
    public int? ProductId { get; set; }
    public string? Name { get; set; }
    public string? Sku { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? LineTotal { get; set; }
}
