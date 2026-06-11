using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Per-promotion per-day aggregates (ingested from orders / POS). Used for list columns and KPIs in a date range.</summary>
public partial class PromotionDailyMetric
{
    [Key]
    public int Id { get; set; }

    public int PromotionId { get; set; }

    [Precision(0)]
    public DateTime MetricDateUtc { get; set; }

    /// <summary>Order source channel at redemption time (web / store / mobile / phone). KPI filter uses this, not <c>Promotion.ChannelsJson</c>.</summary>
    [StringLength(20)]
    public string Channel { get; set; } = "web";

    public int RedemptionsCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RevenueNis { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountNis { get; set; }

    [ForeignKey("PromotionId")]
    [InverseProperty("PromotionDailyMetric")]
    public virtual Promotion Promotion { get; set; } = null!;
}
