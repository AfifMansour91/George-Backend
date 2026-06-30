using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using George.Data;
using George.DB;
using Microsoft.Extensions.DependencyInjection;
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
    public const string EventDeleted = "promotion.deleted";

    /// <summary>Header carrying the hex-encoded HMAC-SHA256 signature when a secret is configured.</summary>
    public const string SignatureHeader = "X-StoreOS-Signature";

    /// <summary>Auth header for WordPress Promotion Engine inbound API.</summary>
    public const string PromengSecretHeader = "X-Promeng-Secret";

    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(3) };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PromotionWebhookDispatcher> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public PromotionWebhookDispatcher(
        IHttpClientFactory httpClientFactory,
        ILogger<PromotionWebhookDispatcher> logger,
        IServiceScopeFactory scopeFactory)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _scopeFactory = scopeFactory;
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

        _ = Task.Run(() => SendAsync(eventName, promotion, url!.Trim(), secret), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <summary>Notify downstream (StoreOS JSON or Promeng DELETE) before a promotion row is removed.</summary>
    public Task FireDeleteAsync(Promotion promotion, Site? site, CancellationToken cancelToken = default)
    {
        if (site is null || string.IsNullOrWhiteSpace(site.PromotionWebhookUrl))
            return Task.CompletedTask;

        var url = site.PromotionWebhookUrl.Trim();
        var secret = site.PromotionWebhookSecret;
        _ = Task.Run(() => SendDeleteAsync(promotion, url, secret), CancellationToken.None);
        return Task.CompletedTask;
    }

    private async Task SendDeleteAsync(Promotion promotion, string url, string? secret)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (IsPromengUrl(url))
            {
                var deleteUrl = BuildPromengDeleteUrl(url, promotion.Id);
                using var msg = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                if (!string.IsNullOrWhiteSpace(secret))
                    msg.Headers.TryAddWithoutValidation(PromengSecretHeader, secret!.Trim());
                var promengOk = await SendWithRetriesAsync(client, msg, $"Promeng DELETE for {promotion.Id} → {deleteUrl}").ConfigureAwait(false);
                if (!promengOk)
                {
                    _logger.LogWarning("Promeng DELETE for {PromotionId} → {Url} failed after {Attempts} attempts",
                        promotion.Id, deleteUrl, MaxAttempts);
                }
                return;
            }

            var body = JsonSerializer.Serialize(new
            {
                @event = EventDeleted,
                promotion_id = promotion.Id,
                promotion = ToWireDto(promotion),
            });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using var postMsg = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            postMsg.Headers.TryAddWithoutValidation("X-StoreOS-Event", EventDeleted);
            if (!string.IsNullOrWhiteSpace(secret))
                postMsg.Headers.TryAddWithoutValidation(SignatureHeader, ComputeHmacSha256Hex(body, secret!));
            var ok = await SendWithRetriesAsync(client, postMsg, $"Promotion delete webhook for {promotion.Id} → {url}").ConfigureAwait(false);
            if (!ok)
            {
                _logger.LogWarning("Promotion delete webhook for {PromotionId} → {Url} failed after {Attempts} attempts",
                    promotion.Id, url, MaxAttempts);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Promotion delete webhook for {PromotionId} → {Url} failed", promotion.Id, url);
        }
    }

    internal static string BuildPromengDeleteUrl(string upsertUrl, int promotionId)
    {
        var baseUrl = upsertUrl.TrimEnd('/');
        return $"{baseUrl}/{PromotionPromengWireMapper.ExternalId(new Promotion { Id = promotionId })}";
    }

    private async Task SendAsync(string eventName, Promotion promotion, string url, string? secret)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(10);

            if (IsPromengUrl(url))
            {
                await SendPromengAsync(client, eventName, promotion, url, secret).ConfigureAwait(false);
                return;
            }

            var body = JsonSerializer.Serialize(new
            {
                @event = eventName,
                promotion_id = promotion.Id,
                promotion = ToWireDto(promotion),
            });

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

            using var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            msg.Headers.TryAddWithoutValidation("X-StoreOS-Event", eventName);
            if (!string.IsNullOrWhiteSpace(secret))
                msg.Headers.TryAddWithoutValidation(SignatureHeader, ComputeHmacSha256Hex(body, secret!));

            var ok = await SendWithRetriesAsync(client, msg, $"Promotion webhook {eventName} for {promotion.Id} → {url}").ConfigureAwait(false);
            if (!ok)
            {
                _logger.LogWarning("Promotion webhook {Event} for {PromotionId} → {Url} failed after {Attempts} attempts",
                    eventName, promotion.Id, url, MaxAttempts);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Promotion webhook {Event} for {PromotionId} → {Url} failed", eventName, promotion.Id, url);
        }
    }

    private async Task SendPromengAsync(HttpClient client, string eventName, Promotion promotion, string url, string? secret)
    {
        var productIds = new HashSet<int>();
        var categoryIds = new HashSet<int>();
        PromotionPromengWireMapper.CollectGeorgeIdsFromPayload(promotion, productIds, categoryIds);

        // Resolve a fresh DI scope: this runs on a fire-and-forget Task.Run after the request
        // scope (and its GeorgeDBContext) has been disposed, so the request-scoped storages
        // cannot be reused — doing so throws "Cannot access a disposed context instance".
        using var scope = _scopeFactory.CreateScope();
        var productStorage = scope.ServiceProvider.GetRequiredService<ProductStorage>();
        var categoryStorage = scope.ServiceProvider.GetRequiredService<CategoryStorage>();

        var productMap = await productStorage
            .GetWooCommerceIdMapForProductIdsAsync(productIds.ToList(), CancellationToken.None)
            .ConfigureAwait(false);
        var categoryMap = await categoryStorage
            .GetWooCommerceIdMapForCategoryIdsAsync(categoryIds.ToList(), CancellationToken.None)
            .ConfigureAwait(false);

        var upsert = PromotionPromengWireMapper.ToUpsertBody(promotion, eventName, productMap, categoryMap);
        var body = JsonSerializer.Serialize(new { promotions = new[] { upsert } });

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };

        using var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        if (!string.IsNullOrWhiteSpace(secret))
            msg.Headers.TryAddWithoutValidation(PromengSecretHeader, secret!.Trim());

        var ok = await SendWithRetriesAsync(client, msg, $"Promeng webhook {eventName} for {promotion.Id} → {url}").ConfigureAwait(false);
        if (!ok)
        {
            _logger.LogWarning("Promeng webhook {Event} for {PromotionId} → {Url} failed after {Attempts} attempts",
                eventName, promotion.Id, url, MaxAttempts);
        }
    }

    private async Task<bool> SendWithRetriesAsync(HttpClient client, HttpRequestMessage request, string logContext)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var attemptRequest = await CloneRequestAsync(request).ConfigureAwait(false);
                using var resp = await client.SendAsync(attemptRequest).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode) return true;
                _logger.LogWarning("{Context} returned {Status} (attempt {Attempt}/{Max})",
                    logContext, (int)resp.StatusCode, attempt, MaxAttempts);
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                _logger.LogWarning(ex, "{Context} threw (attempt {Attempt}/{Max})", logContext, attempt, MaxAttempts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{Context} threw on final attempt", logContext);
                return false;
            }

            if (attempt < MaxAttempts)
                await Task.Delay(RetryDelays[attempt - 1]).ConfigureAwait(false);
        }
        return false;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        foreach (var h in request.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var content = new ByteArrayContent(bytes);
            foreach (var h in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            clone.Content = content;
        }
        return clone;
    }

    internal static bool IsPromengUrl(string url) =>
        url.Contains("promeng/v1", StringComparison.OrdinalIgnoreCase);

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
        priority = p.Priority,
        payload_json = p.PayloadJson,
        creation_time = p.CreationTime,
        updated_date = p.UpdatedDate,
    };
}
