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

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(50)]
    public string? CustomerPhone { get; set; }

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

    [StringLength(100)]
    public string? ExternalOrderId { get; set; }

    /// <summary>Number of bags/cartons packed (set at end of picking when enabled in store).</summary>
    public int? BagsCount { get; set; }

    [ForeignKey("AccountId")]
    [InverseProperty("Order")]
    public virtual Account Account { get; set; } = null!;

    [InverseProperty("Order")]
    public virtual ICollection<OrderItem> OrderItem { get; set; } = new List<OrderItem>();

    [ForeignKey("SiteId")]
    [InverseProperty("Order")]
    public virtual Site Site { get; set; } = null!;
}
