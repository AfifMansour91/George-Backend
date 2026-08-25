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
    /// <summary>User id of the staff member who picked the order (לוקט).</summary>
    public int? PickerUserId { get; set; }
    /// <summary>Display name of the picker (User.FullName at picking time).</summary>
    public string? PickerName { get; set; }
    /// <summary>המטפל: user who created the manual order / first took the order into treatment.</summary>
    public int? HandlerUserId { get; set; }
    /// <summary>Display name of the handler (shown under the order source when Site.ShowOrderHandler).</summary>
    public string? HandlerName { get; set; }
    public int AccountId { get; set; }
    public int SiteId { get; set; }
    /// <summary>Account display name (from Account.Name). Used by voucher header when client does not pass an override.</summary>
    public string? AccountName { get; set; }
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
    public string? CustomerEmail { get; set; }
    public int? CustomerId { get; set; }
    public string? DeliveryAddress { get; set; }
    public string? DeliveryStreet { get; set; }
    public string? DeliveryCity { get; set; }
    public string? DeliveryApartment { get; set; }
    public string? DeliveryFloor { get; set; }
    public string? DeliveryEntranceCode { get; set; }
    /// <summary>Recipient when the order was placed FOR someone else (משלוח עבור); null = the customer receives it.</summary>
    public string? DeliveryRecipientName { get; set; }
    public string? DeliveryRecipientPhone { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryTime { get; set; }
    public DateTime? PickupDate { get; set; }
    public string? PickupTime { get; set; }
    public string? ManagerNote { get; set; }
    public string? CustomerNote { get; set; }
    /// <summary>Permanent manager note from CRM Customer.Notes (not stored on Order).</summary>
    public string? CustomerProfileNote { get; set; }
    public string? DeliveryNote { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? ShippingCost { get; set; }
    public decimal? Total { get; set; }
    /// <summary>Phone/manual order: NIS manual discount after promotions.</summary>
    public decimal? ManualDiscountAmount { get; set; }
    public string? ManualDiscountType { get; set; }
    public decimal? ManualDiscountValue { get; set; }
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
    /// <summary>Cumulative amount refunded (partial or full).</summary>
    public decimal? RefundedAmount { get; set; }
    /// <summary>When the (last) refund/credit was performed.</summary>
    public DateTime? RefundedAt { get; set; }
    public DateTime? PaidAt { get; set; }
    /// <summary>Charge amount Cardcom actually reports for the website transaction (verification inquiry).</summary>
    public decimal? GatewayVerifiedAmount { get; set; }
    /// <summary>When the Cardcom verification inquiry last produced a verdict.</summary>
    public DateTime? GatewayVerifiedAt { get; set; }
    /// <summary>True when Cardcom's charged amount diverges from the order total. Null = not verified.</summary>
    public bool? GatewayAmountMismatch { get; set; }
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
    /// <summary>Website orders: "Giorgio" when Giorgio charges the card at picking (token handed over at checkout); null/"Plugin" = store plugin captures.</summary>
    public string? PaymentCaptureOwner { get; set; }
    public decimal? PaymentAuthorizedAmount { get; set; }
    public string? CardcomLowProfileId { get; set; }
    public int? CustomerPaymentMethodId { get; set; }
    /// <summary>Last 4 digits from Cardcom authorization/charge.</summary>
    public string? CardcomTokenLast4 { get; set; }
    /// <summary>Card brand from Cardcom (e.g. Visa).</summary>
    public string? CardcomCardBrand { get; set; }
    /// <summary>PayPlus document URL when an invoice was issued.</summary>
    public string? PayPlusDocumentUrl { get; set; }
    /// <summary>PayPlus credit note URL after refund.</summary>
    public string? PayPlusRefundDocumentUrl { get; set; }
    /// <summary>Last 4 digits from PayPlus authorization/charge.</summary>
    public string? PayPlusCardLast4 { get; set; }
    /// <summary>Card brand from PayPlus (e.g. Visa).</summary>
    public string? PayPlusCardBrand { get; set; }

    /// <summary>Public Wolt tracking page URL after dispatch.</summary>
    public string? WoltTrackingUrl { get; set; }
    public string? WoltTrackingId { get; set; }
    public string? WoltStatus { get; set; }
    public string? WoltDeliveryId { get; set; }
    public DateTime? WoltDispatchedAt { get; set; }

    /// <summary>Sum of line-level promotion discounts (NIS).</summary>
    public decimal? PromotionDiscountTotal { get; set; }

    public List<OrderItemRes> Items { get; set; } = new();
}
