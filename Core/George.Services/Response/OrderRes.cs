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
    /// <summary>Account display name (from Account.Name). Legacy single-line voucher header.</summary>
    public string? AccountName { get; set; }

    /// <summary>Structured account block for voucher header (name, ח.פ, address, phone, website, optional logo).</summary>
    public VoucherAccountHeaderRes? VoucherAccountHeader { get; set; }
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
    /// <summary>Coupon code(s) from ingest or API (comma-separated when multiple).</summary>
    public string? CouponCode { get; set; }
    /// <summary>Raw ingest <c>status</c> from last Woo payload (e.g. on-hold).</summary>
    public string? ExternalOrderStatusRaw { get; set; }
    /// <summary>Raw gateway <c>paymentMethod</c> code from last ingest (e.g. cod).</summary>
    public string? GatewayPaymentMethodCode { get; set; }
    /// <summary>Payload <c>shippingstorename</c>.</summary>
    public string? ShippingStoreName { get; set; }
    public string? ShippingInfoJson { get; set; }
    public string? ShippingAddressJson { get; set; }
    public string? OrderCustomerJson { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
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
    /// <summary>Merchandise subtotal snapshot when the order was first placed (before picking).</summary>
    public decimal? OriginalSubTotal { get; set; }
    /// <summary>Grand total snapshot when the order was first placed (before picking).</summary>
    public decimal? OriginalTotal { get; set; }
    public string? ExternalOrderId { get; set; }
    /// <summary>Number of bags/cartons packed (set at end of picking).</summary>
    public int? BagsCount { get; set; }
    /// <summary>Payment reference / clearance (legacy; often same as gateway transaction id).</summary>
    public string? PaymentReference { get; set; }
    /// <summary>Invoice number when paid.</summary>
    public string? InvoiceNumber { get; set; }
    /// <summary>Cardcom document URL when an invoice was issued.</summary>
    public string? CardcomDocumentUrl { get; set; }
    /// <summary>Credit note number after refund.</summary>
    public string? RefundInvoiceNumber { get; set; }
    /// <summary>Cardcom credit note URL after refund.</summary>
    public string? CardcomRefundDocumentUrl { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Last payment webhook <c>orderId</c>.</summary>
    public string? GatewayPaymentOrderId { get; set; }
    /// <summary>Last payment webhook <c>externalOrderId</c>.</summary>
    public string? GatewayPaymentExternalOrderId { get; set; }
    /// <summary>Last payment webhook <c>siteId</c> echo.</summary>
    public string? GatewayPaymentSiteId { get; set; }
    /// <summary>Last payment webhook <c>isFinished</c>.</summary>
    public string? IsFinished { get; set; }
    /// <summary>Last payment webhook <c>payment.transactionId</c>.</summary>
    public string? GatewayPaymentTransactionId { get; set; }
    /// <summary>Last payment webhook <c>payment.paymentGateway</c>.</summary>
    public string? PaymentGateway { get; set; }
    /// <summary>Legacy raw JSON from older payment webhooks.</summary>
    public string? CardcomPaymentJson { get; set; }
    /// <summary>Gateway <c>status</c> from payment webhook.</summary>
    public string? ExternalPaymentStatus { get; set; }

    /// <summary>First transition to New (defaults to <see cref="CreationTime"/>).</summary>
    public DateTime? NewAt { get; set; }
    public DateTime? InTreatmentAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string PaymentSettleStatus { get; set; } = "None";
    public decimal? PaymentAuthorizedAmount { get; set; }
    public string? CardcomLowProfileId { get; set; }
    public int? CustomerPaymentMethodId { get; set; }
    /// <summary>Last 4 digits from Cardcom authorization/charge.</summary>
    public string? CardcomTokenLast4 { get; set; }
    /// <summary>Card brand from Cardcom (e.g. Visa).</summary>
    public string? CardcomCardBrand { get; set; }
    public List<OrderItemRes> Items { get; set; } = new();
}
