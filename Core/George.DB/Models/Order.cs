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

    [Precision(0)]
    public DateTime? PaidAt { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Order")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Order")]
    public virtual ICollection<OrderItem> OrderItem { get; set; } = new List<OrderItem>();

    [ForeignKey("SiteId")]
    [InverseProperty("Order")]
    public virtual Site Site { get; set; } = null!;
}
