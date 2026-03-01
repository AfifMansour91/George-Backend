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
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
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
    public int SortOrder { get; set; }
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
}
