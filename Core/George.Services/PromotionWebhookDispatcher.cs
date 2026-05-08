using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Fires promotion lifecycle webhooks (created / updated / ended) to the per-site URL
/// configured under "הגדרות חנות → מבצעים → Webhook URL". Uses fire-and-forget
/// semantics so a slow downstream never blocks the API response — failures are logged.
/// Spec: <c>Sprint4/מבצעים.md</c> "סנכרון מבצעים לאתר ולקיוסק (Webhook)".
/// </summary>
public class PromotionWebhookDispatcher
{
    public const string EventCreated = "promotion.created";
    public const string EventUpdated = "promotion.updated";
    public const string EventEnded = "promotion.ended";

    /// <summary>Header carrying the hex-encoded HMAC-SHA256 signature when a secret is configured.</summary>
    public const string SignatureHeader = "X-StoreOS-Signature";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PromotionWebhookDispatcher> _logger;

    public PromotionWebhookDispatcher(IHttpClientFactory httpClientFactory, ILogger<PromotionWebhookDispatcher> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>Convenience wrapper. <paramref name="site"/> may be null — call is then a no-op.</summary>
    public Task FireAsync(string eventName, Promotion promotion, Site? site, CancellationToken cancelToken = default)
    {
        if (site is null) return Task.CompletedTask;
        return FireAsync(eventName, promotion, site.PromotionWebhookUrl, site.PromotionWebhookSecret, cancelToken);
    }

    public Task FireAsync(string eventName, Promotion promotion, string? url, string? secret, CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || promotion is null || string.IsNullOrWhiteSpace(eventName))
            return Task.CompletedTask;

        // Snake-case wire body matches the spec example bodies (`event`, `promotion_id`, `promotion`).
        var body = JsonSerializer.Serialize(new
        {
            @event = eventName,
            promotion_id = promotion.Id,
            promotion = ToWireDto(promotion),
        });

        // Fire-and-forget: don't await. Use a fresh CancellationToken since the request token
        // belongs to the API call that's about to return.
        _ = Task.Run(() => SendAsync(eventName, promotion.Id, url!.Trim(), secret, body), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task SendAsync(string eventName, int promotionId, string url, string? secret, string body)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

            using var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            msg.Headers.TryAddWithoutValidation("X-StoreOS-Event", eventName);
            if (!string.IsNullOrWhiteSpace(secret))
                msg.Headers.TryAddWithoutValidation(SignatureHeader, ComputeHmacSha256Hex(body, secret!));

            using var resp = await client.SendAsync(msg).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Promotion webhook {Event} for {PromotionId} → {Url} returned {Status}",
                    eventName, promotionId, url, (int)resp.StatusCode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Promotion webhook {Event} for {PromotionId} → {Url} failed", eventName, promotionId, url);
        }
    }

    private static string ComputeHmacSha256Hex(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    /// <summary>
    /// Snake-case wire DTO of a <see cref="Promotion"/> row. Storefronts read this and
    /// the inner <c>payload_json</c> for type-specific rules.
    /// </summary>
    private static object ToWireDto(Promotion p) => new
    {
        id = p.Id,
        guid_id = p.GuidId,
        site_id = p.SiteId,
        type = p.PromotionType,
        name = p.Name,
        is_active = p.IsActive,
        is_draft = p.IsDraft,
        show_badge = p.ShowBadge,
        schedule_start_date_utc = p.ScheduleStartDateUtc,
        schedule_end_date_utc = p.ScheduleEndDateUtc,
        list_discount_kind = p.ListDiscountKind,
        channels_json = p.ChannelsJson,
        coupon_code = p.CouponCode,
        applies_to_summary = p.AppliesToSummary,
        payload_json = p.PayloadJson,
        creation_time = p.CreationTime,
        updated_date = p.UpdatedDate,
    };
}
