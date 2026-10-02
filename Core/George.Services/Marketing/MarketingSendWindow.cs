using System.Globalization;
using George.DB;

namespace George.Services.Marketing;

/// <summary>
/// When a marketing message may go out (spec §5.2): inside the account's daily window, never on Shabbat or a
/// Yom Tov. Shabbat follows real sunset times, not the weekday - Friday 16:30 in December is already Shabbat.
/// A large part of our shops' customers keep tradition; a promo on Shabbat morning is not a bug, it is an
/// unsubscribe. So this is a system default, not a checkbox somebody forgets.
/// </summary>
public static class MarketingSendWindow
{
    /// <summary>Margin around sunset. Wide on purpose: it covers candle-lighting customs (up to 40 min before)
    /// and nightfall opinions (up to 72 min after), plus the sunset spread between Eilat and the north.</summary>
    private static readonly TimeSpan BeforeSunset = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan AfterSunset = TimeSpan.FromMinutes(75);
    /// <summary>On the eve of Shabbat / Yom Tov marketing stops at 15:00 even in summer, when sunset is hours away:
    /// by Friday afternoon the shops are closing and nobody can act on a promo (spec: "Friday 18:00 is deferred to Sunday").</summary>
    private static readonly TimeSpan EveCutoff = new(15, 0, 0);

    // Jerusalem - the reference point for sunset.
    private const double Latitude = 31.778;
    private const double Longitude = 35.235;

    private static readonly HebrewCalendar Hebrew = new();
    private static readonly Lazy<TimeZoneInfo> IsraelTz = new(() =>
        TimeZoneInfo.FindSystemTimeZoneById(OperatingSystem.IsWindows() ? "Israel Standard Time" : "Asia/Jerusalem"));

    public static DateTime ToIsrael(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), IsraelTz.Value);

    public static DateTime FromIsrael(DateTime israelLocal) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(israelLocal, DateTimeKind.Unspecified), IsraelTz.Value);

    /// <summary>UTC instant of the most recent Israel midnight.</summary>
    public static DateTime IsraelMidnightUtc(DateTime utcNow) => FromIsrael(ToIsrael(utcNow).Date);

    public static bool IsAllowed(DateTime utc, MarketingSettings settings) => NextAllowed(utc, settings) == utc;

    /// <summary>The earliest instant ≥ <paramref name="utc"/> at which sending is allowed (returns the input itself when it already is).</summary>
    public static DateTime NextAllowed(DateTime utc, MarketingSettings settings)
    {
        var start = ParseTime(settings.SendWindowStart, new TimeSpan(9, 0, 0));
        var end = ParseTime(settings.SendWindowEnd, new TimeSpan(20, 0, 0));
        if (end <= start)
        {
            start = new TimeSpan(9, 0, 0);
            end = new TimeSpan(20, 0, 0);
        }

        var candidate = utc;
        // Each pass fixes one violation; a long holiday run (Rosh Hashana into Shabbat) needs a few passes.
        for (var i = 0; i < 40; i++)
        {
            var local = ToIsrael(candidate);
            if (local.TimeOfDay < start)
            {
                candidate = FromIsrael(local.Date + start);
                continue;
            }
            if (local.TimeOfDay >= end)
            {
                candidate = FromIsrael(local.Date.AddDays(1) + start);
                continue;
            }
            if (settings.BlockShabbatAndHolidays)
            {
                var holyEnd = HolyIntervalEndUtc(candidate);
                if (holyEnd.HasValue)
                {
                    candidate = holyEnd.Value;
                    continue;
                }
            }
            return candidate;
        }
        return candidate;
    }

    /// <summary>When <paramref name="utc"/> falls inside Shabbat / Yom Tov (with margins): the UTC end of that holy interval; else null.</summary>
    public static DateTime? HolyIntervalEndUtc(DateTime utc)
    {
        var localDate = ToIsrael(utc).Date;
        // The instant can belong to the holy day that starts tonight (eve) or to today's holy day.
        foreach (var day in new[] { localDate, localDate.AddDays(1) })
        {
            if (!IsHolyDay(day))
                continue;
            var eve = day.AddDays(-1);
            var startUtc = Min(SunsetUtc(eve) - BeforeSunset, FromIsrael(eve + EveCutoff));
            var endUtc = CeilToMinute(SunsetUtc(day) + AfterSunset);
            if (utc >= startUtc && utc < endUtc)
                return endUtc;
        }
        return null;
    }

    /// <summary>True when the daytime of this (Israel) civil date is Shabbat or a Yom Tov (Israel custom - one day, Rosh Hashana two).</summary>
    public static bool IsHolyDay(DateTime israelDate)
    {
        if (israelDate.DayOfWeek == DayOfWeek.Saturday)
            return true;

        var year = Hebrew.GetYear(israelDate);
        var month = Hebrew.GetMonth(israelDate);
        var day = Hebrew.GetDayOfMonth(israelDate);

        // .NET HebrewCalendar: 1 = Tishrei ... 6 = Adar (Adar I in a leap year), 7 = Adar II in a leap year.
        var shift = Hebrew.IsLeapYear(year) ? 1 : 0;
        var nisan = 7 + shift;
        var sivan = 9 + shift;

        if (month == 1)
            return day is 1 or 2 or 10 or 15 or 22; // Rosh Hashana, Yom Kippur, Sukkot, Shmini Atzeret
        if (month == nisan)
            return day is 15 or 21;                 // Pesach first and last day
        if (month == sivan)
            return day == 6;                        // Shavuot
        return false;
    }

    /// <summary>Sunset (UTC) in Jerusalem on the given civil date - NOAA solar position algorithm, accurate to about a minute.</summary>
    public static DateTime SunsetUtc(DateTime date)
    {
        var n = date.DayOfYear;
        var gamma = 2 * Math.PI / 365d * (n - 1 + 0.5);
        var eqTime = 229.18 * (0.000075 + 0.001868 * Math.Cos(gamma) - 0.032077 * Math.Sin(gamma)
            - 0.014615 * Math.Cos(2 * gamma) - 0.040849 * Math.Sin(2 * gamma));
        var decl = 0.006918 - 0.399912 * Math.Cos(gamma) + 0.070257 * Math.Sin(gamma)
            - 0.006758 * Math.Cos(2 * gamma) + 0.000907 * Math.Sin(2 * gamma)
            - 0.002697 * Math.Cos(3 * gamma) + 0.00148 * Math.Sin(3 * gamma);

        var latRad = Latitude * Math.PI / 180d;
        var zenith = 90.833 * Math.PI / 180d; // accounts for refraction and the solar disc
        var cosHa = Math.Cos(zenith) / (Math.Cos(latRad) * Math.Cos(decl)) - Math.Tan(latRad) * Math.Tan(decl);
        cosHa = Math.Clamp(cosHa, -1d, 1d);
        var haDeg = Math.Acos(cosHa) * 180d / Math.PI;

        var sunsetMinutesUtc = 720 - 4 * (Longitude - haDeg) - eqTime;
        return DateTime.SpecifyKind(date.Date, DateTimeKind.Utc).AddMinutes(sunsetMinutesUtc);
    }

    private static DateTime Min(DateTime a, DateTime b) => a <= b ? a : b;

    private static DateTime CeilToMinute(DateTime t)
    {
        var floor = new DateTime(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind);
        return floor == t ? t : floor.AddMinutes(1);
    }

    private static TimeSpan ParseTime(string? hhmm, TimeSpan fallback) =>
        TimeSpan.TryParseExact((hhmm ?? string.Empty).Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : fallback;
}
