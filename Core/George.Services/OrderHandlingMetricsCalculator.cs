using George.Services.Response;

namespace George.Services;

/// <summary>Median handling durations (hours) from status timestamps - mirrors shop-manager orderHandlingMetrics.ts.</summary>
public static class OrderHandlingMetricsCalculator
{
    private const double MaxHours = 720;

    public sealed class StatusTimestamps
    {
        public DateTime? NewAt { get; init; }
        public DateTime? InTreatmentAt { get; init; }
        public DateTime? ReadyAt { get; init; }
        public DateTime? CompletedAt { get; init; }
    }

    public static OrderHandlingMetricsRes Compute(IEnumerable<StatusTimestamps> orders)
    {
        var treatment = new List<double>();
        var newToTreatment = new List<double>();
        var readyToDelivery = new List<double>();

        foreach (var ts in orders)
        {
            if (ts.InTreatmentAt.HasValue && ts.ReadyAt.HasValue)
            {
                var h = HoursBetween(ts.InTreatmentAt.Value, ts.ReadyAt.Value);
                if (h.HasValue) treatment.Add(h.Value);
            }

            if (ts.NewAt.HasValue && ts.InTreatmentAt.HasValue)
            {
                var h = HoursBetween(ts.NewAt.Value, ts.InTreatmentAt.Value);
                if (h.HasValue) newToTreatment.Add(h.Value);
            }

            var deliveredAt = ts.CompletedAt;
            if (ts.ReadyAt.HasValue && deliveredAt.HasValue)
            {
                var h = HoursBetween(ts.ReadyAt.Value, deliveredAt.Value);
                if (h.HasValue) readyToDelivery.Add(h.Value);
            }
        }

        return new OrderHandlingMetricsRes
        {
            TreatmentHoursMedian = Median(treatment),
            NewToTreatmentHoursMedian = Median(newToTreatment),
            ReadyToDeliveryHoursMedian = Median(readyToDelivery),
        };
    }

    public static StatusTimestamps FromHistoryRows(
        IEnumerable<(string Status, DateTime OccurredAt)> rows,
        DateTime creationTime,
        DateTime? updatedDate)
    {
        var sorted = rows
            .OrderBy(r => r.OccurredAt)
            .ToList();

        static DateTime? Pick(IEnumerable<(string Status, DateTime OccurredAt)> list, string status) =>
            list.Where(r => string.Equals(r.Status, status, StringComparison.OrdinalIgnoreCase))
                .Select(r => (DateTime?)r.OccurredAt)
                .FirstOrDefault();

        return new StatusTimestamps
        {
            NewAt = Pick(sorted, "New") ?? creationTime,
            InTreatmentAt = Pick(sorted, "InTreatment"),
            ReadyAt = Pick(sorted, "Ready"),
            CompletedAt = Pick(sorted, "Completed") ?? updatedDate,
        };
    }

    private static double? HoursBetween(DateTime start, DateTime end)
    {
        var h = (end.ToUniversalTime() - start.ToUniversalTime()).TotalHours;
        if (h <= 0 || h > MaxHours) return null;
        return h;
    }

    private static double? Median(List<double> values)
    {
        if (values.Count == 0) return null;
        values.Sort();
        return values[values.Count / 2];
    }
}
