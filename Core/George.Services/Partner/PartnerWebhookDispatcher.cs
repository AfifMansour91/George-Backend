using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace George.Services.Partner;

/// <summary>
/// Outbound order-event webhooks for Partner API integrations (docs/PARTNER_API.md "Webhooks").
/// Fires <c>order.status_changed</c> / <c>order.payment_changed</c> / <c>order.delivery_changed</c> to the site's
/// <see cref="Site.PartnerWebhookUrl"/> for orders whose Source is WhatsApp / Partner - never for website, kiosk
/// or phone orders, so a partner only ever sees the orders it placed itself.
/// Fire-and-forget with its own DI scope (the request scope is gone by the time the POST runs), 3 attempts,
/// HMAC-SHA256 signature when a secret is configured, every attempt logged to IntegrationLog
/// (entity <c>order</c>, operation <c>partner/webhook</c>) so support can replay what the partner received.
/// Registered as a singleton.
/// </summary>
public sealed class PartnerWebhookDispatcher
{
    public const string EventStatusChanged = "order.status_changed";
    public const string EventPaymentChanged = "order.payment_changed";
    public const string EventDeliveryChanged = "order.delivery_changed";
    public const string EventTest = "webhook.test";

    public const string EventHeader = "X-Partner-Event";
    public const string DeliveryHeader = "X-Partner-Delivery";
    public const string SignatureHeader = "X-Partner-Signature";
    public const string LogOperation = "partner/webhook";

    private const int MaxAttempts = 3;
    private static readonly TimeSpan[] RetryDelays = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(10) };
    private static readonly TimeSpan FingerprintTtl = TimeSpan.FromHours(6);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PartnerWebhookDispatcher> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIntegrationLogQueue _logQueue;

    /// <summary>Last payload fingerprint sent per (orderId, event) - payment state is saved several times per charge, the partner needs one event per real change.</summary>
    private readonly ConcurrentDictionary<string, (string Fingerprint, DateTime SentAtUtc)> _lastSent = new();

    public PartnerWebhookDispatcher(
        IHttpClientFactory httpClientFactory,
        ILogger<PartnerWebhookDispatcher> logger,
        IServiceScopeFactory scopeFactory,
        IIntegrationLogQueue logQueue)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _scopeFactory = scopeFactory;
        _logQueue = logQueue;
    }

    /// <summary>Queue an order event. Returns immediately; nothing is sent when the order is not partner-sourced or the site has no webhook URL.</summary>
    public void FireOrderEvent(int orderId, string eventName)
    {
        if (orderId <= 0 || string.IsNullOrWhiteSpace(eventName)) return;
        _ = Task.Run(() => SendOrderEventAsync(orderId, eventName), CancellationToken.None);
    }

    /// <summary>Admin "test webhook": one synchronous POST with a sample payload, result returned to the caller.</summary>
    public async Task<PartnerWebhookTestRes> SendTestAsync(Site site, CancellationToken cancelToken)
    {
        var res = new PartnerWebhookTestRes();
        if (string.IsNullOrWhiteSpace(site.PartnerWebhookUrl))
        {
            res.Error = "No webhook URL configured for this site.";
            return res;
        }
        var payload = new WebhookEnvelope
        {
            Event = EventTest,
            EventId = Guid.NewGuid().ToString("N"),
            OccurredAt = DateTime.UtcNow,
            SiteId = site.Id,
            Order = new PartnerOrderRes
            {
                OrderId = 0,
                OrderNumber = "TEST",
                Source = PartnerOrderMapper.SourceWhatsApp,
                Status = "New",
                PaymentStatus = "Unpaid",
                DeliveryType = "Pickup",
                CustomerName = "בדיקה",
                CreatedAt = DateTime.UtcNow,
            },
        };
        var body = JsonSerializer.Serialize(payload, JsonOptions);
        var sw = Stopwatch.StartNew();
        var (ok, status, error) = await PostOnceAsync(site.PartnerWebhookUrl!, site.PartnerWebhookSecret, EventTest, payload.EventId, body, cancelToken)
            .ConfigureAwait(false);
        sw.Stop();
        res.Sent = ok;
        res.HttpStatus = status;
        res.Error = error;
        res.DurationMs = (int)sw.ElapsedMilliseconds;
        Log(site.Id, null, EventTest, site.PartnerWebhookUrl!, body, status, ok, error, res.DurationMs, attempt: 1);
        return res;
    }

    private async Task SendOrderEventAsync(int orderId, string eventName)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orderStorage = scope.ServiceProvider.GetRequiredService<OrderStorage>();
            var siteStorage = scope.ServiceProvider.GetRequiredService<SiteStorage>();

            var order = await orderStorage.GetOrderByIdAsync(orderId, CancellationToken.None).ConfigureAwait(false);
            if (order == null || !PartnerOrderMapper.IsPartnerSource(order.Source)) return;

            var site = await siteStorage.GetSiteAsync(order.SiteId, CancellationToken.None).ConfigureAwait(false);
            var url = site?.PartnerWebhookUrl?.Trim();
            if (site == null || string.IsNullOrWhiteSpace(url)) return;

            var history = await orderStorage
                .GetStatusHistoryByOrderIdsAsync(new[] { orderId }, CancellationToken.None)
                .ConfigureAwait(false);
            var mapped = PartnerOrderMapper.Map(order, history.GetValueOrDefault(orderId), alreadyExisted: false);

            if (!ShouldSend(orderId, eventName, Fingerprint(eventName, mapped)))
                return;

            var envelope = new WebhookEnvelope
            {
                Event = eventName,
                EventId = Guid.NewGuid().ToString("N"),
                OccurredAt = DateTime.UtcNow,
                SiteId = site.Id,
                Order = mapped,
            };
            var body = JsonSerializer.Serialize(envelope, JsonOptions);

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                var sw = Stopwatch.StartNew();
                var (ok, status, error) = await PostOnceAsync(url!, site.PartnerWebhookSecret, eventName, envelope.EventId, body, CancellationToken.None)
                    .ConfigureAwait(false);
                sw.Stop();
                Log(site.Id, orderId, eventName, url!, body, status, ok, error, (int)sw.ElapsedMilliseconds, attempt);
                if (ok) return;
                if (attempt < MaxAttempts)
                    await Task.Delay(RetryDelays[attempt - 1]).ConfigureAwait(false);
            }
            _logger.LogWarning("Partner webhook {Event} for order {OrderId} → {Url} failed after {Attempts} attempts",
                eventName, orderId, url, MaxAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Partner webhook {Event} for order {OrderId} failed", eventName, orderId);
        }
    }

    private async Task<(bool Ok, int? Status, string? Error)> PostOnceAsync(
        string url, string? secret, string eventName, string eventId, string body, CancellationToken cancelToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            using var msg = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            msg.Headers.TryAddWithoutValidation(EventHeader, eventName);
            msg.Headers.TryAddWithoutValidation(DeliveryHeader, eventId);
            if (!string.IsNullOrWhiteSpace(secret))
                msg.Headers.TryAddWithoutValidation(SignatureHeader, "sha256=" + ComputeHmacSha256Hex(body, secret!.Trim()));
            using var resp = await client.SendAsync(msg, cancelToken).ConfigureAwait(false);
            var status = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode) return (true, status, null);
            var text = await SafeReadAsync(resp).ConfigureAwait(false);
            return (false, status, Truncate($"HTTP {status}: {text}", 1000));
        }
        catch (Exception ex)
        {
            return (false, null, Truncate(ex.GetType().Name + ": " + ex.Message, 1000));
        }
    }

    private void Log(int siteId, int? orderId, string eventName, string url, string body, int? status, bool ok, string? error, int durationMs, int attempt)
    {
        _logQueue.TryEnqueue(new IntegrationLog
        {
            SiteId = siteId,
            EntityType = IntegrationLogEntityType.Order.ToWire(),
            EntityId = orderId,
            Direction = IntegrationLogDirection.Outbound.ToWire(),
            Operation = LogOperation,
            Level = ok ? IntegrationLogLevel.Info.ToWire() : (attempt >= MaxAttempts ? IntegrationLogLevel.Error.ToWire() : IntegrationLogLevel.Warning.ToWire()),
            Url = Truncate($"{eventName} → {url}", 1000),
            HttpStatus = status,
            Success = ok,
            RequestJson = body,
            ResponseBody = ok ? null : error,
            DurationMs = durationMs,
            Error = ok ? null : Truncate($"attempt {attempt}/{MaxAttempts}: {error}", 1000),
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private bool ShouldSend(int orderId, string eventName, string fingerprint)
    {
        var now = DateTime.UtcNow;
        if (_lastSent.Count > 5000)
        {
            foreach (var kv in _lastSent)
                if (now - kv.Value.SentAtUtc > FingerprintTtl) _lastSent.TryRemove(kv.Key, out _);
        }
        var key = $"{orderId}|{eventName}";
        if (_lastSent.TryGetValue(key, out var last) && last.Fingerprint == fingerprint && now - last.SentAtUtc < FingerprintTtl)
            return false;
        _lastSent[key] = (fingerprint, now);
        return true;
    }

    private static string Fingerprint(string eventName, PartnerOrderRes o) => eventName switch
    {
        EventPaymentChanged => $"{o.PaymentStatus}|{o.PaymentSettleStatus}|{o.PaymentMethod}|{o.PaidAt:O}|{o.InvoiceUrl}|{o.Total}",
        EventDeliveryChanged => $"{o.DeliveryStatus}|{o.DeliveryTrackingLink}",
        _ => $"{o.Status}|{o.Total}",
    };

    private static async Task<string> SafeReadAsync(HttpResponseMessage resp)
    {
        try { return (await resp.Content.ReadAsStringAsync().ConfigureAwait(false)) ?? ""; }
        catch { return ""; }
    }

    private static string? Truncate(string? s, int max) =>
        s == null ? null : (s.Length <= max ? s : s.Substring(0, max));

    private static string ComputeHmacSha256Hex(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private sealed class WebhookEnvelope
    {
        public string Event { get; set; } = null!;
        public string EventId { get; set; } = null!;
        public DateTime OccurredAt { get; set; }
        public int SiteId { get; set; }
        public PartnerOrderRes? Order { get; set; }
    }
}
