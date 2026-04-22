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

    public int RedemptionsCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RevenueNis { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountNis { get; set; }

    [ForeignKey("PromotionId")]
    [InverseProperty("PromotionDailyMetric")]
    public virtual Promotion Promotion { get; set; } = null!;
}
