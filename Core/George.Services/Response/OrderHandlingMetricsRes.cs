namespace George.Services.Response;

/// <summary>Aggregated handling-time medians for dashboard (hours).</summary>
public class OrderHandlingMetricsRes
{
    public double? TreatmentHoursMedian { get; set; }
    public double? NewToTreatmentHoursMedian { get; set; }
    public double? ReadyToDeliveryHoursMedian { get; set; }
}
