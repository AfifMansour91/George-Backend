using George.Services;

namespace George.Services.Tests;

public class ReportPeriodRangeTests
{
    [Fact]
    public void TodayRange_UsesIsraelMidnightNotUtcMidnight()
    {
        // 2024-06-02 20:00 UTC = 2024-06-02 23:00 Israel (still same Israel day)
        var utcNow = new DateTime(2024, 6, 2, 20, 0, 0, DateTimeKind.Utc);
        var (fromUtc, toUtcExclusive) = ReportPeriodRange.ResolveCurrentRange("today", null, null, utcNow);

        var israelToday = ReportPeriodRange.IsraelCalendarToday(utcNow);
        Assert.Equal(new DateTime(2024, 6, 2), israelToday);

        // Israel 2024-06-02 00:00 = 2024-06-01 21:00 UTC (IDT, UTC+3)
        Assert.Equal(new DateTime(2024, 6, 1, 21, 0, 0, DateTimeKind.Utc), fromUtc);
        Assert.Equal(new DateTime(2024, 6, 2, 21, 0, 0, DateTimeKind.Utc), toUtcExclusive);
    }

    [Fact]
    public void PreviousIsraelDayRange_IsOneDayBeforeToday()
    {
        var utcNow = new DateTime(2024, 6, 2, 20, 0, 0, DateTimeKind.Utc);
        var (todayFrom, todayTo) = ReportPeriodRange.ResolveCurrentRange("today", null, null, utcNow);
        var (prevFrom, prevTo) = ReportPeriodRange.ResolvePreviousIsraelDayRange(utcNow);

        Assert.Equal(todayFrom - prevFrom, todayTo - todayFrom);
        Assert.Equal(prevTo, todayFrom);
    }

    [Fact]
    public void WeekRange_StartsOnSundayIsrael()
    {
        // 2024-06-05 Wed Israel
        var utcNow = new DateTime(2024, 6, 5, 12, 0, 0, DateTimeKind.Utc);
        var (fromUtc, toUtcExclusive) = ReportPeriodRange.ResolveCurrentRange("week", null, null, utcNow);
        var spanDays = (toUtcExclusive - fromUtc).TotalDays;
        Assert.Equal(7, spanDays);
    }
}
