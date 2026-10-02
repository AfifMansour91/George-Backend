using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using George.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace George.Services.Marketing;

/// <summary>
/// Inforu delivery reports (DLR). Every marketing SMS goes out with <c>CustomerMessageID</c> = the delivery id and
/// <c>DeliveryNotificationUrl</c> = our webhook, and Inforu posts back one JSON object per message
/// (apidoc.inforu.co.il → SMS → Utilities → Delivery Notification → Push option using json).
/// Status: 2 delivered · -2 not delivered · -4 blocked by Inforu · 6 clicked (their shortener - ignored, we track our own links).
/// </summary>
public class MarketingDeliveryReportService
{
    public const int StatusDelivered = 2;
    public const int StatusNotDelivered = -2;
    public const int StatusBlocked = -4;

    private readonly MarketingStorage _storage;
    private readonly ILogger<MarketingDeliveryReportService> _logger;

    public MarketingDeliveryReportService(MarketingStorage storage, ILogger<MarketingDeliveryReportService> logger)
    {
        _storage = storage;
        _logger = logger;
    }

    /// <summary>Shared secret carried in the webhook URL. <c>Sms:DlrSecret</c>, else derived from the JWT key so every environment has one without extra config.</summary>
    public static string? WebhookSecret(IConfiguration configuration)
    {
        var configured = configuration["Sms:DlrSecret"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.Trim();
        var jwtKey = configuration["Auth:Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            return null;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("inforu-dlr:" + jwtKey));
        return Convert.ToHexString(hash, 0, 16).ToLowerInvariant();
    }

    /// <summary>The URL Inforu posts delivery reports to; null when the API has no public base URL configured.</summary>
    public static string? WebhookUrl(IConfiguration configuration)
    {
        var baseUrl = configuration["Payment:PublicApiBaseUrl"] ?? configuration["App:ApiPublicBaseUrl"];
        var secret = WebhookSecret(configuration);
        if (string.IsNullOrWhiteSpace(baseUrl) || secret == null)
            return null;
        return $"{baseUrl.Trim().TrimEnd('/')}/Webhooks/Sms/Inforu?s={secret}";
    }

    public static bool IsValidSecret(IConfiguration configuration, string? secret)
    {
        var expected = WebhookSecret(configuration);
        return expected != null && !string.IsNullOrEmpty(secret)
            && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(secret));
    }

    public sealed class Report
    {
        public long DeliveryId { get; init; }
        public int Status { get; init; }
        public string? Description { get; init; }
        public DateTime? At { get; init; }
    }

    /// <summary>
    /// Lenient parse: the documented shape is a bare JSON array, but a wrapping <c>{"Data":[...]}</c> or a single object
    /// are accepted too. Items without a numeric <c>CustomerMessageId</c> are not ours (operational SMS carry none) and are dropped.
    /// </summary>
    public static List<Report> Parse(string payload)
    {
        var reports = new List<Report>();
        if (string.IsNullOrWhiteSpace(payload))
            return reports;

        using var doc = JsonDocument.Parse(payload);
        var root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("Data", out var data))
            root = data;

        IEnumerable<JsonElement> items = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray()
            : root.ValueKind == JsonValueKind.Object ? new[] { root } : Array.Empty<JsonElement>();

        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;
            var id = ReadLong(item, "CustomerMessageId") ?? ReadLong(item, "CustomerMessageID");
            var status = ReadLong(item, "Status");
            if (id is not > 0 || status == null)
                continue;
            reports.Add(new Report
            {
                DeliveryId = id.Value,
                Status = (int)status.Value,
                Description = ReadString(item, "StatusDescription"),
                At = ReadDate(item, "NotificationDate") ?? ReadDate(item, "InsertDate"),
            });
        }
        return reports;
    }

    public async Task<int> ApplyAsync(string payload, CancellationToken cancelToken)
    {
        List<Report> reports;
        try
        {
            reports = Parse(payload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Inforu DLR: payload is not JSON ({Length} chars).", payload?.Length ?? 0);
            return 0;
        }

        var applied = 0;
        foreach (var r in reports)
        {
            var delivered = r.Status == StatusDelivered;
            var failed = r.Status is StatusNotDelivered or StatusBlocked;
            if (!delivered && !failed)
                continue;
            var error = failed ? Truncate($"לא נמסר: {r.Description ?? r.Status.ToString()}", 500) : null;
            if (await _storage.ApplyDeliveryReportAsync(r.DeliveryId, delivered, error, r.At ?? DateTime.UtcNow, cancelToken).ConfigureAwait(false))
                applied++;
        }
        if (reports.Count > 0)
            _logger.LogInformation("Inforu DLR: {Applied}/{Total} reports applied.", applied, reports.Count);
        return applied;
    }

    private static long? ReadLong(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt64(out var n))
            return n;
        if (p.ValueKind == JsonValueKind.String && long.TryParse(p.GetString(), out var s))
            return s;
        return null;
    }

    private static string? ReadString(JsonElement item, string name) =>
        item.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTime? ReadDate(JsonElement item, string name)
    {
        var s = ReadString(item, name);
        if (s == null) return null;
        // Inforu timestamps are Israel local time without an offset, day-first. Never parse with the server culture
        // (an en-US host would read 05/09 as May 9th).
        var formats = new[] { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ss.fff", "yyyy-MM-dd HH:mm:ss", "dd.MM.yyyy HH:mm:ss" };
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        if (DateTime.TryParseExact(s, formats, inv, System.Globalization.DateTimeStyles.None, out var d)
            || DateTime.TryParse(s, System.Globalization.CultureInfo.GetCultureInfo("he-IL"), System.Globalization.DateTimeStyles.None, out d)
            || DateTime.TryParse(s, inv, System.Globalization.DateTimeStyles.None, out d))
            return MarketingSendWindow.FromIsrael(d);
        return null;
    }

    private static string Truncate(string s, int max) => s.Length > max ? s.Substring(0, max) : s;
}
