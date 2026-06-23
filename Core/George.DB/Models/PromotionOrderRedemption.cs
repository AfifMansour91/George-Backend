using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>
/// One row per (site, order, promotion) — the idempotency anchor for promotion redemptions.
/// Whether the redemption is learned when the order is created/synced (<c>Source=order</c>) or
/// from the WooCommerce Promeng <c>/redemptions</c> report (<c>Source=external</c>), it is
/// counted exactly once: a metric delta is applied only when the row is newly inserted, so a
/// re-sent order or a duplicate redemption report can never inflate <see cref="PromotionDailyMetric"/>.
/// Spec: <c>shop-manager/docs/wooCommerceEngines/ORDER_PROMOTION_SYNC_SPEC.md</c>.
/// </summary>
public partial class PromotionOrderRedemption
{
    [Key]
    public int Id { get; set; }

    public int SiteId { get; set; }

    /// <summary>George order this redemption belongs to — the dedup anchor (with SiteId + PromotionId).</summary>
    public int OrderId { get; set; }

    /// <summary>WooCommerce order id/number (matches <c>Order.ExternalOrderId</c>); helper for matching external /redemptions reports to the order. Null for native orders.</summary>
    [StringLength(100)]
    public string? ExternalOrderId { get; set; }

    public int PromotionId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>Net revenue on the discounted lines (line total − discount). Stored so a reversal subtracts the exact KPI it added. 0 for external reports where line revenue is unknown.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal RevenueNis { get; set; }

    /// <summary>Redemption channel (web / store / mobile / phone) — mirrors <see cref="PromotionDailyMetric.Channel"/>.</summary>
    [StringLength(20)]
    public string Channel { get; set; } = "web";

    [Precision(0)]
    public DateTime RedeemedAtUtc { get; set; }

    [Precision(0)]
    public DateTime RecordedAtUtc { get; set; }

    /// <summary><c>order</c> (stamped from the order payload) or <c>external</c> (from the Promeng /redemptions report).</summary>
    [StringLength(20)]
    public string Source { get; set; } = "order";
}
