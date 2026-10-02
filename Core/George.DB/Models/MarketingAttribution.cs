using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>An order credited to a marketing delivery. At most one attribution per order (unique OrderId).</summary>
[Index("OrderId", Name = "UQ_MarketingAttribution_OrderId", IsUnique = true)]
[Index("SendId", Name = "IX_MarketingAttribution_SendId")]
public partial class MarketingAttribution
{
    [Key]
    public long Id { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    public int AccountId { get; set; }

    public int SendId { get; set; }

    public long DeliveryId { get; set; }

    public int OrderId { get; set; }

    public int? CustomerId { get; set; }

    /// <summary><c>window</c> (estimated - ordered within the window) | <c>coupon</c> (exact - personal code redeemed).</summary>
    [StringLength(20)]
    public string Source { get; set; } = null!;

    public int WindowHours { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Revenue { get; set; }

    [Precision(0)]
    public DateTime OrderCreatedAt { get; set; }
}
