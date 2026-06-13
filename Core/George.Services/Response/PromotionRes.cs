namespace George.Services.Response;

public class PromotionRes
{
    public int Id { get; set; }
    public Guid GuidId { get; set; }
    public int SiteId { get; set; }
    public string PromotionType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool ShowBadge { get; set; }
    public bool IsDraft { get; set; }
    public DateTime? ScheduleStartDateUtc { get; set; }
    public DateTime? ScheduleEndDateUtc { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreationTime { get; set; }
    public DateTime? UpdatedDate { get; set; }

    public string ListDiscountKind { get; set; } = "percent";
    public string? ChannelsJson { get; set; }
    public string? CouponCode { get; set; }
    public string? AppliesToSummary { get; set; }

    public int Priority { get; set; } = 10;

    public int PeriodRedemptions { get; set; }
    public decimal PeriodRevenueNis { get; set; }
    public decimal PeriodDiscountNis { get; set; }
}
