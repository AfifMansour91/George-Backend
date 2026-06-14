using System.Text.Json;

namespace George.Services;

/// <summary>
/// Weekday gates for promotions (<c>payload.daysOfWeek</c>). Uses Israel calendar day
/// so storefront / catalog behavior matches local business hours.
/// </summary>
public static class PromotionWeekdaySchedule
{
    public static bool PayloadAllowsToday(JsonElement payload, DateTime utcNow)
    {
        if (!payload.TryGetProperty("daysOfWeek", out var d) || d.ValueKind != JsonValueKind.Array) return true;
        if (d.GetArrayLength() == 0) return true;

        var todayKey = ToWeekdayKey(ReportPeriodRange.IsraelCalendarToday(utcNow).DayOfWeek);
        foreach (var el in d.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.String) continue;
            if (string.Equals(el.GetString(), todayKey, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    public static bool PayloadAllowsToday(string? payloadJson, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(payloadJson)) return true;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            return PayloadAllowsToday(doc.RootElement, utcNow);
        }
        catch (JsonException)
        {
            return true;
        }
    }

    internal static string ToWeekdayKey(DayOfWeek dow) =>
        dow switch
        {
            DayOfWeek.Sunday => "Sun",
            DayOfWeek.Monday => "Mon",
            DayOfWeek.Tuesday => "Tue",
            DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday => "Thu",
            DayOfWeek.Friday => "Fri",
            DayOfWeek.Saturday => "Sat",
            _ => string.Empty,
        };
}
