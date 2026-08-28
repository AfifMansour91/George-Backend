using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace George.DB;

/// <summary>Store promotion definition (discount / buy X pay Y / buy X get Y). Rules JSON in <see cref="PayloadJson"/>.</summary>
public partial class Promotion
{
    [Key]
    public int Id { get; set; }

    public bool IsDeleted { get; set; }

    public Guid GuidId { get; set; }

    [Precision(0)]
    public DateTime CreationTime { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }

    public int? CreationUserId { get; set; }

    public int? UpdateUserId { get; set; }

    public int SiteId { get; set; }

    /// <summary>discount | buy_x_pay_y | buy_x_get_y</summary>
    [StringLength(40)]
    public string PromotionType { get; set; } = null!;

    [StringLength(500)]
    public string Name { get; set; } = null!;

    /// <summary>When true, promotion applies in storefront channels (subject to schedule/rules).</summary>
    public bool IsActive { get; set; }

    public bool ShowBadge { get; set; }

    /// <summary>Incomplete required fields - shown under Drafts tab.</summary>
    public bool IsDraft { get; set; }

    /// <summary>Optional start of validity (UTC date). Denormalized for list tabs; full rules remain in <see cref="PayloadJson"/>.</summary>
    [Precision(0)]
    public DateTime? ScheduleStartDateUtc { get; set; }

    /// <summary>Optional end of validity (UTC date, inclusive display). Null = no end.</summary>
    [Precision(0)]
    public DateTime? ScheduleEndDateUtc { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string PayloadJson { get; set; } = "{}";

    /// <summary>UI discount kind: percent | amount | gift | bxgy | bxpy (for filters and badges).</summary>
    [StringLength(20)]
    public string ListDiscountKind { get; set; } = "percent";

    /// <summary>JSON array of channel keys, e.g. ["web","mobile","store"].</summary>
    [StringLength(500)]
    public string? ChannelsJson { get; set; }

    [StringLength(100)]
    public string? CouponCode { get; set; }

    [StringLength(500)]
    public string? AppliesToSummary { get; set; }

    /// <summary>Lower runs first when several promotions apply. Default 10.</summary>
    public int Priority { get; set; } = 10;

    [ForeignKey("SiteId")]
    [InverseProperty("Promotion")]
    public virtual Site Site { get; set; } = null!;

    [InverseProperty("Promotion")]
    public virtual ICollection<PromotionDailyMetric> PromotionDailyMetric { get; set; } = new List<PromotionDailyMetric>();
}
