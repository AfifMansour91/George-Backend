namespace George.Services
{
    /// <summary>
    /// Shared report period presets (Israel calendar): today, Sun–Sat week, full month, last month, custom.
    /// </summary>
    public static class ReportPeriodRange
    {
        private static readonly TimeZoneInfo IsraelTimeZone = ResolveIsraelTimeZone();

        private static TimeZoneInfo ResolveIsraelTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Israel Standard Time");
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Jerusalem");
            }
        }

        private static DateTime AssumeUtc(DateTime dt) =>
            dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

        public static DateTime IsraelCalendarToday(DateTime utcNow) =>
            TimeZoneInfo.ConvertTimeFromUtc(AssumeUtc(utcNow), IsraelTimeZone).Date;

        public static (DateTime fromUtc, DateTime toUtcExclusive) ResolveCurrentRange(
            string period,
            DateTime? customFrom,
            DateTime? customTo,
            DateTime utcNow)
        {
            var p = (period ?? "month").Trim().ToLowerInvariant();
            var today = IsraelCalendarToday(utcNow);
            return p switch
            {
                "today" => IsraelLocalRangeToUtc(today, today.AddDays(1)),
                "week" => ResolveWeekRange(today),
                "month" => ResolveCurrentMonthRange(today),
                "last_month" or "prev_month" or "previous_month" => ResolveLastMonthRange(today),
                "custom" when customFrom != null && customTo != null =>
                    (DateTime.SpecifyKind(customFrom.Value.Date, DateTimeKind.Utc),
                        DateTime.SpecifyKind(customTo.Value.Date.AddDays(1), DateTimeKind.Utc)),
                _ => ResolveCurrentMonthRange(today),
            };
        }

        /// <summary>שבוע קלנדרי בישראל: ראשון 00:00 עד מוצאי שבת (כולל).</summary>
        private static (DateTime fromUtc, DateTime toUtcExclusive) ResolveWeekRange(DateTime todayIsrael)
        {
            var sunday = todayIsrael.AddDays(-(int)todayIsrael.DayOfWeek);
            var saturday = sunday.AddDays(6);
            return IsraelLocalRangeToUtc(sunday, saturday.AddDays(1));
        }

        /// <summary>חודש נוכחי: מאחד בחודש עד סוף החודש (ישראל).</summary>
        private static (DateTime fromUtc, DateTime toUtcExclusive) ResolveCurrentMonthRange(DateTime todayIsrael)
        {
            var monthStart = new DateTime(todayIsrael.Year, todayIsrael.Month, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);
            return IsraelLocalRangeToUtc(monthStart, monthEnd.AddDays(1));
        }

        /// <summary>חודש שעבר: מאחד עד סוף החודש הקודם (ישראל).</summary>
        private static (DateTime fromUtc, DateTime toUtcExclusive) ResolveLastMonthRange(DateTime todayIsrael)
        {
            var thisMonthStart = new DateTime(todayIsrael.Year, todayIsrael.Month, 1);
            var prevMonthStart = thisMonthStart.AddMonths(-1);
            return IsraelLocalRangeToUtc(prevMonthStart, thisMonthStart);
        }

        /// <summary>Previous Israel calendar day [00:00, 24:00).</summary>
        public static (DateTime fromUtc, DateTime toUtcExclusive) ResolvePreviousIsraelDayRange(DateTime utcNow)
        {
            var today = IsraelCalendarToday(utcNow);
            return IsraelLocalRangeToUtc(today.AddDays(-1), today);
        }

        /// <summary>Same weekday one week earlier in Israel calendar.</summary>
        public static (DateTime fromUtc, DateTime toUtcExclusive) ResolveSameWeekdayIsraelDayRange(DateTime utcNow)
        {
            var today = IsraelCalendarToday(utcNow);
            var day = today.AddDays(-7);
            return IsraelLocalRangeToUtc(day, day.AddDays(1));
        }

        /// <summary>Previous period of equal length (for week/month/custom KPI compare).</summary>
        public static (DateTime fromUtc, DateTime toUtcExclusive) ResolvePreviousPeriodRange(
            DateTime fromUtc,
            DateTime toUtcExclusive)
        {
            var span = toUtcExclusive - fromUtc;
            return (fromUtc - span, fromUtc);
        }

        /// <summary>Shift an entire UTC window back by calendar days (same weekday compare).</summary>
        public static (DateTime fromUtc, DateTime toUtcExclusive) ShiftRangeByDays(
            DateTime fromUtc,
            DateTime toUtcExclusive,
            int days)
        {
            return (fromUtc.AddDays(days), toUtcExclusive.AddDays(days));
        }

        private static (DateTime fromUtc, DateTime toUtcExclusive) IsraelLocalRangeToUtc(
            DateTime fromLocal, DateTime toLocalExclusive)
        {
            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified), IsraelTimeZone);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(
                DateTime.SpecifyKind(toLocalExclusive, DateTimeKind.Unspecified), IsraelTimeZone);
            return (fromUtc, toUtc);
        }
    }
}
