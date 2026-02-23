using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Sprint 2: Order from website, kiosk, or phone. Multi-site: each site sees its own orders.</summary>
[Table("Order")]
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

    /// <summary>Display number per site (e.g. 1001, 1002).</summary>
    [StringLength(50)]
    public string OrderNumber { get; set; } = null!;

    /// <summary>Website | Kiosk | Phone</summary>
    [StringLength(20)]
    public string Source { get; set; } = null!;

    /// <summary>New | InTreatment | Ready | Completed | Cancelled</summary>
    [StringLength(20)]
    public string Status { get; set; } = "New";

    /// <summary>Shipping | Pickup</summary>
    [StringLength(20)]
    public string? DeliveryType { get; set; }

    /// <summary>Unpaid | Paid | Captured</summary>
    [StringLength(20)]
    public string PaymentStatus { get; set; } = "Unpaid";

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(50)]
    public string? CustomerPhone { get; set; }

    /// <summary>Optional FK to client/customer table (future).</summary>
    public int? CustomerId { get; set; }

    [StringLength(500)]
    public string? DeliveryAddress { get; set; }

    [Precision(0)]
    public DateTime? DeliveryDate { get; set; }

    [StringLength(20)]
    public string? DeliveryTime { get; set; }

    [Precision(0)]
    public DateTime? PickupDate { get; set; }

    [StringLength(20)]
    public string? PickupTime { get; set; }

    /// <summary>Manager note for this order (shown in yellow on card).</summary>
    [StringLength(2000)]
    public string? ManagerNote { get; set; }

    /// <summary>Customer note from checkout / manual entry.</summary>
    [StringLength(2000)]
    public string? CustomerNote { get; set; }

    /// <summary>Delivery/pickup note (e.g. arrival time).</summary>
    [StringLength(500)]
    public string? DeliveryNote { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ShippingCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Total { get; set; }

    /// <summary>External id (e.g. WooCommerce order id) for sync.</summary>
    [StringLength(100)]
    public string? ExternalOrderId { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Orders")]
    public virtual Account Account { get; set; } = null!;

    [ForeignKey("SiteId")]
    [InverseProperty("Orders")]
    public virtual Site Site { get; set; } = null!;

    [InverseProperty("Order")]
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
