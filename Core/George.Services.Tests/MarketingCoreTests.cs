using George.Providers;
using George.Data;
using George.DB;
using George.Services.Marketing;
using Xunit;

namespace George.Services.Tests;

public class SmsUnitsTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("Hello", 1)]
    public void Basic(string text, int expected) => Assert.Equal(expected, SmsUnits.Calculate(text));

    [Fact]
    public void Gsm7_160_is_one_161_is_two()
    {
        Assert.Equal(1, SmsUnits.Calculate(new string('a', 160)));
        Assert.Equal(2, SmsUnits.Calculate(new string('a', 161)));
        Assert.Equal(2, SmsUnits.Calculate(new string('a', 306)));
        Assert.Equal(3, SmsUnits.Calculate(new string('a', 307)));
    }

    [Fact]
    public void Gsm7_extended_chars_cost_two_septets()
    {
        // 80 x '€' = 160 septets -> still one message; one more tips it over.
        Assert.Equal(1, SmsUnits.Calculate(new string('€', 80)));
        Assert.Equal(2, SmsUnits.Calculate(new string('€', 81)));
    }

    [Fact]
    public void Hebrew_is_ucs2_70_then_67_per_part()
    {
        Assert.Equal(1, SmsUnits.Calculate(new string('א', 70)));
        Assert.Equal(2, SmsUnits.Calculate(new string('א', 71)));
        Assert.Equal(2, SmsUnits.Calculate(new string('א', 134)));
        Assert.Equal(3, SmsUnits.Calculate(new string('א', 135)));
    }

    [Fact]
    public void Spec_screen_example_142_hebrew_chars_is_three_messages()
    {
        Assert.Equal(3, SmsUnits.Calculate(new string('ש', 142)));
    }

    [Fact]
    public void Emoji_counts_as_two_utf16_units()
    {
        // 69 Hebrew + one astral emoji (2 units) = 71 units -> two parts.
        Assert.Equal(2, SmsUnits.Calculate(new string('א', 69) + "🐟"));
    }
}

public class MarketingSendWindowTests
{
    private static readonly MarketingSettings Defaults = new();

    private static DateTime Il(int y, int m, int d, int h, int min = 0) =>
        MarketingSendWindow.FromIsrael(new DateTime(y, m, d, h, min, 0));

    [Fact]
    public void Weekday_inside_window_is_allowed()
    {
        var t = Il(2026, 9, 16, 10); // Wednesday
        Assert.True(MarketingSendWindow.IsAllowed(t, Defaults));
        Assert.Equal(t, MarketingSendWindow.NextAllowed(t, Defaults));
    }

    [Fact]
    public void Before_window_moves_to_window_start_same_day()
    {
        Assert.Equal(Il(2026, 9, 16, 9), MarketingSendWindow.NextAllowed(Il(2026, 9, 16, 7, 30), Defaults));
    }

    [Fact]
    public void After_window_moves_to_next_morning()
    {
        Assert.Equal(Il(2026, 9, 17, 9), MarketingSendWindow.NextAllowed(Il(2026, 9, 16, 20), Defaults));
    }

    [Fact]
    public void Spec_edge_case_friday_1800_is_deferred_to_sunday_0900()
    {
        // 28.8.2026 is a Friday; the spec's example: "תוזמנה לשישי 18:00 ← נדחית לראשון 09:00".
        Assert.Equal(Il(2026, 8, 30, 9), MarketingSendWindow.NextAllowed(Il(2026, 8, 28, 18), Defaults));
    }

    [Fact]
    public void Friday_morning_is_allowed_friday_winter_afternoon_is_not()
    {
        Assert.True(MarketingSendWindow.IsAllowed(Il(2026, 12, 18, 10), Defaults));
        // December sunset in Jerusalem is ~16:37 - by 16:00 we are inside the pre-Shabbat margin.
        Assert.False(MarketingSendWindow.IsAllowed(Il(2026, 12, 18, 16), Defaults));
    }

    [Fact]
    public void Summer_saturday_lands_on_sunday_morning()
    {
        // Shabbat is out after 20:00 in August - past the daily window - so the next slot is Sunday 09:00.
        Assert.Equal(Il(2026, 8, 30, 9), MarketingSendWindow.NextAllowed(Il(2026, 8, 29, 12), Defaults));
    }

    [Fact]
    public void Winter_saturday_lands_on_saturday_night_on_a_whole_minute()
    {
        // December: Shabbat + margin ends before 20:00, so Saturday night is a legitimate slot.
        var next = MarketingSendWindow.ToIsrael(MarketingSendWindow.NextAllowed(Il(2026, 12, 19, 12), Defaults));
        Assert.Equal(new DateTime(2026, 12, 19), next.Date);
        Assert.InRange(next.TimeOfDay, new TimeSpan(17, 40, 0), new TimeSpan(18, 5, 0));
        Assert.Equal(0, next.Second);
    }

    [Fact]
    public void Winter_saturday_night_after_margin_is_allowed_again()
    {
        // Shabbat is out ~17:40 in December, +75 min margin => 19:00 is inside the window and allowed.
        Assert.True(MarketingSendWindow.IsAllowed(Il(2026, 12, 19, 19, 30), Defaults));
    }

    [Fact]
    public void Shabbat_block_can_be_switched_off()
    {
        var settings = new MarketingSettings { BlockShabbatAndHolidays = false };
        Assert.True(MarketingSendWindow.IsAllowed(Il(2026, 12, 19, 12), settings));
    }

    [Theory]
    [InlineData(2026, 9, 12)]  // Rosh Hashana 5787 day 1 (also Shabbat)
    [InlineData(2026, 9, 13)]  // Rosh Hashana day 2
    [InlineData(2026, 9, 21)]  // Yom Kippur
    [InlineData(2026, 9, 26)]  // Sukkot
    [InlineData(2026, 10, 3)]  // Shmini Atzeret / Simchat Torah
    [InlineData(2027, 4, 22)]  // Pesach first day
    [InlineData(2027, 4, 28)]  // Pesach seventh day
    [InlineData(2027, 6, 11)]  // Shavuot
    [InlineData(2024, 4, 23)]  // Pesach in a Hebrew LEAP year (5784) - month numbering shifts
    [InlineData(2024, 6, 12)]  // Shavuot in a leap year
    public void Known_holy_days(int y, int m, int d) => Assert.True(MarketingSendWindow.IsHolyDay(new DateTime(y, m, d)));

    [Theory]
    [InlineData(2026, 9, 14)]  // Monday after Rosh Hashana
    [InlineData(2026, 9, 27)]  // Chol HaMoed Sukkot
    [InlineData(2027, 4, 25)]  // Chol HaMoed Pesach
    [InlineData(2024, 3, 25)]  // 15 Adar II 5784 - would be "Pesach" if the leap shift were ignored
    public void Ordinary_days(int y, int m, int d) => Assert.False(MarketingSendWindow.IsHolyDay(new DateTime(y, m, d)));

    [Fact]
    public void Rosh_hashana_eve_into_two_day_holiday_lands_after_it()
    {
        // Friday 11.9.2026 17:00 = erev Rosh Hashana (Sat+Sun). Next allowed: Monday 14.9 09:00.
        Assert.Equal(Il(2026, 9, 14, 9), MarketingSendWindow.NextAllowed(Il(2026, 9, 11, 17), Defaults));
    }

    [Fact]
    public void Sunset_is_close_to_published_jerusalem_times()
    {
        // Published: 21.6.2026 ~19:47 IDT, 21.12.2026 ~16:40 IST.
        var june = MarketingSendWindow.ToIsrael(MarketingSendWindow.SunsetUtc(new DateTime(2026, 6, 21)));
        var december = MarketingSendWindow.ToIsrael(MarketingSendWindow.SunsetUtc(new DateTime(2026, 12, 21)));
        Assert.InRange(june.TimeOfDay, new TimeSpan(19, 40, 0), new TimeSpan(19, 55, 0));
        Assert.InRange(december.TimeOfDay, new TimeSpan(16, 32, 0), new TimeSpan(16, 48, 0));
    }
}

public class MarketingMessageRendererTests
{
    [Fact]
    public void Replaces_tokens_and_appends_unsubscribe_line()
    {
        var text = MarketingMessageRenderer.Render("שלום [customer_name], מבצע ב[store_name]: [link]", "דנה לוי", "מעדני גורמה", "https://g.co/s/abc", "https://g.co/x/abc");
        Assert.Equal("שלום דנה, מבצע במעדני גורמה: https://g.co/s/abc\nלהסרה: https://g.co/x/abc", text);
    }

    [Fact]
    public void Link_without_placeholder_is_appended_on_its_own_line()
    {
        var text = MarketingMessageRenderer.Render("מבצע!", "דנה", "חנות", "https://g.co/s/abc", "https://g.co/x/abc");
        Assert.Equal("מבצע!\nhttps://g.co/s/abc\nלהסרה: https://g.co/x/abc", text);
    }

    [Fact]
    public void Missing_name_does_not_leave_a_dangling_comma_space()
    {
        var text = MarketingMessageRenderer.Render("שלום [customer_name], מה נשמע", null, null, null, "u");
        Assert.StartsWith("שלום, מה נשמע", text);
    }

    [Fact]
    public void Tokens_are_valid_and_differ()
    {
        var a = MarketingMessageRenderer.NewToken();
        var b = MarketingMessageRenderer.NewToken();
        Assert.True(MarketingMessageRenderer.IsValidToken(a));
        Assert.NotEqual(a, b);
        Assert.False(MarketingMessageRenderer.IsValidToken("../etc"));
        Assert.False(MarketingMessageRenderer.IsValidToken(null));
    }
}

public class InforuContractTests
{
    [Theory]
    [InlineData("GiorgioShop", true)]
    [InlineData("*Giorgio", true)]
    [InlineData("0549999999", true)]
    [InlineData("מעדני גורמה", false)]
    [InlineData("My Shop", false)]
    [InlineData("TwelveChars1", false)]
    [InlineData("", false)]
    public void Sender_rule(string sender, bool ok) => Assert.Equal(ok, AccountSmsService.IsValidInforuSender(sender));

    [Fact]
    public void Dlr_payload_parses_documented_shapes()
    {
        var documented = "[{\"InforuId\":\"SQLQ1_x\",\"PhoneNumber\":\"0543266290\",\"Status\":2,\"StatusDescription\":\"Delivered\",\"CustomerMessageId\":\"123\",\"SegmentsNumber\":1,\"NotificationDate\":\"2024-09-30T13:26:00\"},"
            + "{\"PhoneNumber\":\"0501\",\"Status\":-2,\"StatusDescription\":\"Rejected by operator\",\"CustomerMessageId\":124},"
            + "{\"PhoneNumber\":\"0502\",\"Status\":2,\"CustomerMessageId\":\"\"}]";
        var reports = MarketingDeliveryReportService.Parse(documented);
        Assert.Equal(2, reports.Count);
        Assert.Equal((123L, 2), (reports[0].DeliveryId, reports[0].Status));
        Assert.Equal(MarketingSendWindow.FromIsrael(new DateTime(2024, 9, 30, 13, 26, 0)), reports[0].At);
        Assert.Equal((124L, -2, "Rejected by operator"), (reports[1].DeliveryId, reports[1].Status, reports[1].Description));

        Assert.Single(MarketingDeliveryReportService.Parse("{\"Data\":[{\"Status\":2,\"CustomerMessageId\":7}]}"));
        Assert.Single(MarketingDeliveryReportService.Parse("{\"Status\":-4,\"CustomerMessageId\":\"8\"}"));
        Assert.Empty(MarketingDeliveryReportService.Parse(""));
    }

    [Fact]
    public void Quota_exceeded_codes()
    {
        Assert.True(SmsSendResult.Fail("x", -13).QuotaExceeded);
        Assert.True(SmsSendResult.Fail("x", -15).QuotaExceeded);
        Assert.False(SmsSendResult.Fail("x", -21).QuotaExceeded);
        Assert.False(SmsSendResult.Ok().QuotaExceeded);
    }
}

public class MarketingSegmentDefinitionTests
{
    [Fact]
    public void Every_system_segment_is_valid()
    {
        foreach (var (key, conditions) in MarketingSystemSegments.All)
            Assert.True(MarketingSegmentQuery.Validate(conditions, allowSystemAxes: true) == null, key);
    }

    [Fact]
    public void Risk_axis_is_not_available_to_saved_segments()
    {
        Assert.NotNull(MarketingSegmentQuery.Validate(MarketingSystemSegments.Find(MarketingSystemSegments.AtRisk)));
    }

    [Fact]
    public void More_than_three_conditions_is_rejected()
    {
        var c = new SegmentCondition { Axis = SegmentAxis.Frequency, Operator = SegmentOperator.Gte, Value = "1" };
        Assert.NotNull(MarketingSegmentQuery.Validate(new[] { c, c, c, c }));
        Assert.Null(MarketingSegmentQuery.Validate(new[] { c, c, c }));
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var conditions = new List<SegmentCondition>
        {
            new() { Axis = SegmentAxis.Product, Operator = SegmentOperator.BoughtCategory, Value = "12", Days = 90 },
            new() { Axis = SegmentAxis.Geo, Operator = SegmentOperator.CityIs, Value = "אשדוד" },
        };
        var parsed = MarketingSegmentQuery.Parse(MarketingSegmentQuery.Serialize(conditions));
        Assert.Equal(2, parsed.Count);
        Assert.Equal(90, parsed[0].Days);
        Assert.Equal("אשדוד", parsed[1].Value);
        Assert.Empty(MarketingSegmentQuery.Parse("not json"));
    }

    [Theory]
    [InlineData("0501234567", true)]
    [InlineData("0771234567", false)]  // landline / VoIP - cannot receive SMS
    [InlineData("050123456", false)]
    [InlineData("", false)]
    public void Sms_capable_phone(string phone, bool expected) => Assert.Equal(expected, MarketingStorage.IsSmsCapablePhone(phone));
}

public class MarketingSendErrorTextTests
{
    [Theory]
    [InlineData(-13, "Inforu -13: quota", "מכסת ההודעות אצל הספק נגמרה — Inforu -13: quota")]
    [InlineData(-21, "Inforu -21: sender", "שם השולח לא מאושר אצל הספק — Inforu -21: sender")]
    [InlineData(null, "ActiveTrail HTTP 503", "הספק לא היה זמין — ActiveTrail HTTP 503")]
    [InlineData(null, null, "הספק דחה את ההודעה")]
    public void Provider_failures_are_described_in_Hebrew_with_the_raw_text_kept(int? statusId, string? raw, string expected)
    {
        var result = George.Providers.SmsSendResult.Fail(raw ?? string.Empty, statusId);
        if (raw == null) result.Error = null;
        Assert.Equal(expected, George.Services.Marketing.MarketingDispatchService.DescribeSendError(result));
    }
}

public class MarketingResendAndEditRulesTests
{
    [Fact]
    public void Resend_axis_is_system_only_and_needs_a_source_send()
    {
        var ok = new[] { new George.Data.SegmentCondition { Axis = George.Data.SegmentAxis.Resend, Operator = George.Data.SegmentOperator.NonBuyers, Value = "12" } };
        // A shop cannot type it into a saved segment...
        Assert.NotNull(George.Data.MarketingSegmentQuery.Validate(ok));
        // ...but the service may build it.
        Assert.Null(George.Data.MarketingSegmentQuery.Validate(ok, allowSystemAxes: true));
        var bad = new[] { new George.Data.SegmentCondition { Axis = George.Data.SegmentAxis.Resend, Operator = George.Data.SegmentOperator.NonBuyers, Value = "0" } };
        Assert.NotNull(George.Data.MarketingSegmentQuery.Validate(bad, allowSystemAxes: true));
    }

    [Fact]
    public void Scheduled_send_is_editable_only_until_ten_minutes_before()
    {
        var now = new DateTime(2026, 10, 2, 8, 0, 0, DateTimeKind.Utc);
        var send = new George.DB.MarketingSend { Status = George.Data.MarketingStorage.SendStatus.Scheduled, ScheduledAt = now.AddMinutes(30) };
        Assert.True(George.Services.Marketing.MarketingService.IsEditable(send, now));
        send.ScheduledAt = now.AddMinutes(9);
        Assert.False(George.Services.Marketing.MarketingService.IsEditable(send, now));
        send.ScheduledAt = now.AddMinutes(30);
        send.StartedAt = now;
        Assert.False(George.Services.Marketing.MarketingService.IsEditable(send, now));
        send.StartedAt = null;
        send.Status = George.Data.MarketingStorage.SendStatus.Sending;
        Assert.False(George.Services.Marketing.MarketingService.IsEditable(send, now));
    }
}
