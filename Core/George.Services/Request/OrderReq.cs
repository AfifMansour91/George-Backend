using System.ComponentModel.DataAnnotations;

namespace George.Services.Request;

/// <summary>Sprint 2: Create order request (manual or from ingest).</summary>
public class CreateOrderReq
{
    /// <summary>Optional. If not set, resolved from SiteId.</summary>
    public int AccountId { get; set; }
    [Required]
    public int SiteId { get; set; }
    public string? OrderNumber { get; set; }
    public string Source { get; set; } = "Website";
    public string Status { get; set; } = "New";
    public string? DeliveryType { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    /// <summary>Phone manual: Cash, SavedCard, etc. WooCommerce: mapped from gateway (e.g. cod → Cash).</summary>
    public string? PaymentMethod { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    /// <summary>Kiosk: customer consent for marketing SMS. When set, persisted on the customer record.</summary>
    public bool? MarketingSms { get; set; }
    public int? CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTime { get; set; }
    public DateTime? PickupDate { get; set; }
    public string? PickupTime { get; set; }
    public string? ManagerNote { get; set; }
    public string? CustomerNote { get; set; }
    public string? DeliveryNote { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? Total { get; set; }
    public string? ExternalOrderId { get; set; }

    /// <summary>WooCommerce ingest: raw gateway title, labels, notes (optional for manual).</summary>
    public string? PaymentMethodTitle { get; set; }
    public string? PaymentLabel { get; set; }
    public string? ShippingLabel { get; set; }
    public string? BillingNotes { get; set; }
    public string? InternalOrderNotes { get; set; }
    public string? WooCommerceSiteId { get; set; }
    public string? WooCommercePickupAffiliateId { get; set; }

    public List<CreateOrderItemReq> Items { get; set; } = new();
}

public class CreateOrderItemReq
{
    public int? ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? Title { get; set; }
    public string? VariantTitle { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitWeightGrams { get; set; }
    public decimal? PricePerUnit { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? Notes { get; set; }
    /// <summary>WooCommerce line snapshot.</summary>
    public string? SaleUnits { get; set; }
    public string? SaleTotalWeight { get; set; }
    public int? WooCommerceProductId { get; set; }
    public int? WooCommerceVariationId { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>Sprint 2: Add items to existing order (picking "הוסף פריט"). POST Order/{orderId}/Items.</summary>
public class AddOrderItemsReq
{
    public List<CreateOrderItemReq> Items { get; set; } = new();
}

/// <summary>Sprint 2: Save picking state (שמור וצא). PUT Order/{id}/Picking.</summary>
public class UpdatePickingReq
{
    public List<UpdatePickingItemReq> Items { get; set; } = new();
}

public class UpdatePickingItemReq
{
    public int OrderItemId { get; set; }
    public decimal? PickedQuantity { get; set; }
    public decimal? TotalPrice { get; set; }
}

/// <summary>Sprint 2: Update order (status, notes, delivery).</summary>
public class UpdateOrderReq
{
    public string? Status { get; set; }
    public string? ManagerNote { get; set; }
    public string? CustomerNote { get; set; }
    public string? DeliveryNote { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTime { get; set; }
    public DateTime? PickupDate { get; set; }
    public string? PickupTime { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? PaymentStatus { get; set; }
    public string? PaymentMethod { get; set; }
    /// <summary>Number of bags/cartons packed (set at end of picking).</summary>
    public int? BagsCount { get; set; }
}
