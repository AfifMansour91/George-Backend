namespace George.Services.Response;

/// <summary>Sprint 2: Order response.</summary>
public class OrderRes
{
    public int Id { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public int? CreationUserId { get; set; }
    public int? UpdateUserId { get; set; }
    public int AccountId { get; set; }
    public int SiteId { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string Source { get; set; } = null!;
    public string Status { get; set; } = "New";
    public string? DeliveryType { get; set; }
    public string PaymentStatus { get; set; } = "Unpaid";
    public string? PaymentMethod { get; set; }
    public string? PaymentMethodTitle { get; set; }
    public string? PaymentLabel { get; set; }
    public string? ShippingLabel { get; set; }
    public string? BillingNotes { get; set; }
    public string? InternalOrderNotes { get; set; }
    public string? WooCommerceSiteId { get; set; }
    public string? WooCommercePickupAffiliateId { get; set; }
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
    /// <summary>Number of bags/cartons packed (set at end of picking).</summary>
    public int? BagsCount { get; set; }
    public List<OrderItemRes> Items { get; set; } = new();
}
