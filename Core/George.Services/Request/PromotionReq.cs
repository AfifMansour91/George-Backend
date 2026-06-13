using System.ComponentModel.DataAnnotations;

namespace George.Services.Request;

public class CreatePromotionReq
{
    [Required]
    public int SiteId { get; set; }

    /// <summary>discount | buy_x_pay_y | buy_x_get_y</summary>
    [Required]
    [StringLength(40)]
    public string PromotionType { get; set; } = null!;

    [Required]
    [StringLength(500)]
    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool ShowBadge { get; set; }

    public bool IsDraft { get; set; } = true;

    /// <summary>Optional schedule for list tabs / reporting (full rules in payload).</summary>
    public DateTime? ScheduleStartDateUtc { get; set; }

    public DateTime? ScheduleEndDateUtc { get; set; }

    public string PayloadJson { get; set; } = "{}";

    [StringLength(20)]
    public string? ListDiscountKind { get; set; }

    [StringLength(500)]
    public string? ChannelsJson { get; set; }

    [StringLength(100)]
    public string? CouponCode { get; set; }

    [StringLength(500)]
    public string? AppliesToSummary { get; set; }

    /// <summary>Lower runs first when several promotions apply. Default 10.</summary>
    public int Priority { get; set; } = 10;
}

public class UpdatePromotionReq
{
    [StringLength(40)]
    public string? PromotionType { get; set; }

    [StringLength(500)]
    public string? Name { get; set; }

    public bool? IsActive { get; set; }

    public bool? ShowBadge { get; set; }

    public bool? IsDraft { get; set; }

    public int? Priority { get; set; }

    public DateTime? ScheduleStartDateUtc { get; set; }

    public DateTime? ScheduleEndDateUtc { get; set; }

    public string? PayloadJson { get; set; }

    [StringLength(20)]
    public string? ListDiscountKind { get; set; }

    [StringLength(500)]
    public string? ChannelsJson { get; set; }

    [StringLength(100)]
    public string? CouponCode { get; set; }

    [StringLength(500)]
    public string? AppliesToSummary { get; set; }
}

public class UpdatePromotionStatusReq
{
    [Required]
    public bool IsActive { get; set; }
}
