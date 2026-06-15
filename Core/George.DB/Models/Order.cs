using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

public partial class Order
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    public int AccountId { get; set; }

    public int SiteId { get; set; }

    [StringLength(50)]
    public string OrderNumber { get; set; } = null!;

    [StringLength(20)]
    public string Source { get; set; } = null!;

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [StringLength(20)]
    public string? DeliveryType { get; set; }

    [StringLength(20)]
    public string PaymentStatus { get; set; } = null!;

    /// <summary>How the customer pays (e.g. Cash, SavedCard, cod→Cash from WooCommerce).</summary>
    [StringLength(50)]
    public string? PaymentMethod { get; set; }

    /// <summary>WooCommerce gateway title (e.g. "תשלום בעת מסירה").</summary>
    [StringLength(200)]
    public string? PaymentMethodTitle { get; set; }

    /// <summary>WooCommerce <c>payment_label</c> (e.g. "לשליח").</summary>
    [StringLength(150)]
    public string? PaymentLabel { get; set; }

    /// <summary>WooCommerce <c>shipping_label</c> (e.g. "איסוף עצמי").</summary>
    [StringLength(150)]
    public string? ShippingLabel { get; set; }

    /// <summary>Coupon code(s) applied on the order (from WooCommerce ingest). Comma-separated when multiple.</summary>
    [StringLength(100)]
    public string? CouponCode { get; set; }

    /// <summary>WooCommerce <c>billing_notes</c> (קופה).</summary>
    [StringLength(2000)]
    public string? BillingNotes { get; set; }

    /// <summary>WooCommerce / plugin internal notes (status history, sync).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? InternalOrderNotes { get; set; }

    /// <summary>Echo of WooCommerce payload <c>siteId</c> when sent.</summary>
    [StringLength(50)]
    public string? WooCommerceSiteId { get; set; }

    /// <summary>WooCommerce <c>shippingInfo.pickupAffiliateId</c>.</summary>
    [StringLength(50)]
    public string? WooCommercePickupAffiliateId { get; set; }

    /// <summary>Last JSON body from WooCommerce <c>POST /WooCommerce/Order</c> (audit/support; not used by the app).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? WooCommerceRequestJson { get; set; }

    /// <summary>Raw <c>status</c> from ingest payload (e.g. WooCommerce on-hold) before our status mapping.</summary>
    [StringLength(50)]
    public string? ExternalOrderStatusRaw { get; set; }

    /// <summary>Raw gateway <c>paymentMethod</c> code from ingest (e.g. cod) before internal mapping.</summary>
    [StringLength(100)]
    public string? GatewayPaymentMethodCode { get; set; }

    /// <summary>Ingest <c>shippingstorename</c> (pickup branch display name).</summary>
    [StringLength(200)]
    public string? ShippingStoreName { get; set; }

    /// <summary>Serialized <c>shippingInfo</c> from last ingest payload.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ShippingInfoJson { get; set; }

    /// <summary>Serialized <c>shippingAddress</c> from last ingest payload.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ShippingAddressJson { get; set; }

    /// <summary>Serialized <c>customer</c> object from last ingest payload.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? OrderCustomerJson { get; set; }

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(50)]
    public string? CustomerPhone { get; set; }

    [StringLength(200)]
    public string? CustomerEmail { get; set; }

    public int? CustomerId { get; set; }

    [ForeignKey("CustomerId")]
    [InverseProperty("Order")]
    public virtual Customer? Customer { get; set; }

    [StringLength(500)]
    public string? DeliveryAddress { get; set; }

    /// <summary>Street and building number (separate from city for shop-manager / CRM).</summary>
    [StringLength(400)]
    public string? DeliveryStreet { get; set; }

    [StringLength(120)]
    public string? DeliveryCity { get; set; }

    [StringLength(64)]
    public string? DeliveryApartment { get; set; }

    [StringLength(32)]
    public string? DeliveryFloor { get; set; }

    [StringLength(64)]
    public string? DeliveryEntranceCode { get; set; }

    [Precision(0)]
    public DateTime? DeliveryDate { get; set; }

    [StringLength(20)]
    public string? DeliveryTime { get; set; }

    [Precision(0)]
    public DateTime? PickupDate { get; set; }

    [StringLength(20)]
    public string? PickupTime { get; set; }

    [StringLength(2000)]
    public string? ManagerNote { get; set; }

    [StringLength(2000)]
    public string? CustomerNote { get; set; }

    [StringLength(500)]
    public string? DeliveryNote { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? SubTotal { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ShippingCost { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? Total { get; set; }

    /// <summary>Phone/manual order: NIS amount deducted by staff (after promotion discounts).</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ManualDiscountAmount { get; set; }

    /// <summary>Phone/manual order: <c>percent</c> or <c>amount</c> — how ManualDiscountValue was entered.</summary>
    [StringLength(20)]
    public string? ManualDiscountType { get; set; }

    /// <summary>Phone/manual order: entered percent (e.g. 10) or fixed NIS amount before capping.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? ManualDiscountValue { get; set; }

    /// <summary>Subtotal at order creation (before picking adjusts line weights/prices). Immutable after first save.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? OriginalSubTotal { get; set; }

    /// <summary>Grand total at order creation (before picking). Immutable after first save.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? OriginalTotal { get; set; }

    [StringLength(100)]
    public string? ExternalOrderId { get; set; }

    /// <summary>Number of bags/cartons packed (set at end of picking when enabled in store).</summary>
    public int? BagsCount { get; set; }

    /// <summary>Payment reference / clearance number (from WooCommerce or payment provider).</summary>
    [StringLength(100)]
    public string? PaymentReference { get; set; }

    /// <summary>Invoice number when order is paid.</summary>
    [StringLength(100)]
    public string? InvoiceNumber { get; set; }

    /// <summary>Cardcom document PDF/view URL when an invoice was issued.</summary>
    public string? CardcomDocumentUrl { get; set; }

    /// <summary>Credit note / refund invoice number after Cardcom refund document.</summary>
    [StringLength(100)]
    public string? RefundInvoiceNumber { get; set; }

    /// <summary>Cardcom credit note PDF/view URL after refund.</summary>
    [StringLength(1000)]
    public string? CardcomRefundDocumentUrl { get; set; }

    /// <summary>Last payment webhook <c>orderId</c> (WooCommerce / gateway echo).</summary>
    [StringLength(64)]
    public string? GatewayPaymentOrderId { get; set; }

    /// <summary>Last payment webhook <c>externalOrderId</c>.</summary>
    [StringLength(64)]
    public string? GatewayPaymentExternalOrderId { get; set; }

    /// <summary>Last payment webhook <c>siteId</c> echo (API key still authorizes site).</summary>
    [StringLength(50)]
    public string? GatewayPaymentSiteId { get; set; }

    /// <summary>Last payment webhook <c>isFinished</c> (e.g. charged label).</summary>
    [StringLength(200)]
    public string? IsFinished { get; set; }

    /// <summary>Last payment webhook <c>payment.transactionId</c>.</summary>
    [StringLength(120)]
    public string? GatewayPaymentTransactionId { get; set; }

    /// <summary>Last payment webhook <c>payment.paymentGateway</c> (any provider).</summary>
    [StringLength(200)]
    public string? PaymentGateway { get; set; }

    [Precision(0)]
    public DateTime? PaidAt { get; set; }

    /// <summary>When true, one-time catalog stock deduction for lines without saved picking has already run when the order reached Completed.</summary>
    public bool CompletionInventoryApplied { get; set; }

    /// <summary>Legacy: raw gateway JSON from older webhooks; prefer gateway payment columns.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? CardcomPaymentJson { get; set; }

    /// <summary>Gateway-reported <c>status</c> from payment webhook (e.g. success / failed).</summary>
    [StringLength(100)]
    public string? ExternalPaymentStatus { get; set; }

    /// <summary>Provider-agnostic payment lifecycle (None, Initiated, Authorized, Captured, etc.).</summary>
    [StringLength(40)]
    public string PaymentSettleStatus { get; set; } = "None";

    /// <summary>Amount authorized at checkout (J5 hold).</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? PaymentAuthorizedAmount { get; set; }

    [StringLength(64)]
    public string? CardcomLowProfileId { get; set; }

    [StringLength(32)]
    public string? CardcomSuspendedDealId { get; set; }

    [StringLength(32)]
    public string? CardcomApprovalNumber { get; set; }

    [StringLength(8)]
    public string? CardcomTokenLast4 { get; set; }

    [StringLength(32)]
    public string? CardcomCardBrand { get; set; }

    public int? CustomerPaymentMethodId { get; set; }

    /// <summary>Public Wolt tracking page URL after dispatch.</summary>
    [StringLength(1000)]
    public string? WoltTrackingUrl { get; set; }

    /// <summary>Short Wolt tracking code (SMS-friendly).</summary>
    [StringLength(64)]
    public string? WoltTrackingId { get; set; }

    /// <summary>Wolt courier state (e.g. INFO_RECEIVED, PICKED_UP, DELIVERED).</summary>
    [StringLength(64)]
    public string? WoltStatus { get; set; }

    /// <summary>Wolt internal delivery id (24 hex chars).</summary>
    [StringLength(64)]
    public string? WoltDeliveryId { get; set; }

    [Precision(0)]
    public DateTime? WoltDispatchedAt { get; set; }

    /// <summary>Last successful dispatch API response snapshot (audit / refresh).</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? WoltDeliveryJson { get; set; }

    [ForeignKey(nameof(CustomerPaymentMethodId))]
    public virtual CustomerPaymentMethod? CustomerPaymentMethod { get; set; }

    [InverseProperty("Order")]
    public virtual ICollection<OrderPaymentEvent> OrderPaymentEvent { get; set; } = new List<OrderPaymentEvent>();

    [ForeignKey("AccountId")]
    [InverseProperty("Order")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Order")]
    public virtual ICollection<OrderItem> OrderItem { get; set; } = new List<OrderItem>();

    [InverseProperty("Order")]
    public virtual ICollection<OrderStatusHistory> OrderStatusHistory { get; set; } = new List<OrderStatusHistory>();

    [ForeignKey("SiteId")]
    [InverseProperty("Order")]
    public virtual Site Site { get; set; } = null!;
}
