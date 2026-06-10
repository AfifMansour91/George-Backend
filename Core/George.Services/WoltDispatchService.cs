using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Calls the OC Wolt Drive WordPress dispatch API
/// (<c>POST {WooCommerceUrl}/wp-json/ocws-wolt/v1/dispatch</c>).
/// </summary>
public class WoltDispatchService : ServiceBase
{
    private static readonly TimeSpan DispatchHttpTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan ProbeHttpTimeout = TimeSpan.FromSeconds(15);

    private readonly OrderStorage _orderStorage;
    private readonly SiteStorage _siteStorage;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMapper _mapper;

    public WoltDispatchService(
        ILogger<WoltDispatchService> logger,
        IMapper mapper,
        CacheManager cache,
        OrderStorage orderStorage,
        SiteStorage siteStorage,
        IHttpClientFactory httpClientFactory)
        : base(logger, mapper, cache)
    {
        _orderStorage = orderStorage;
        _siteStorage = siteStorage;
        _httpClientFactory = httpClientFactory;
        _mapper = mapper;
    }

    /// <summary>
    /// Probes <c>GET {WooCommerceUrl}/wp-json/ed/v1/wolt</c> — returns whether the Wolt plugin endpoint exists and reports active.
    /// </summary>
    public async Task<IApiResponse<WoltPluginProbeRes>> ProbeWoltPluginAsync(int siteId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<WoltPluginProbeRes> { Data = new WoltPluginProbeRes() };

        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        if (string.IsNullOrWhiteSpace(site.WooCommerceUrl))
        {
            response.Data.ErrorMessage = "WooCommerce site URL is not configured.";
            return response;
        }

        var probeUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/ed/v1/wolt";
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = ProbeHttpTimeout;

        try
        {
            var httpResponse = await httpClient.GetAsync(probeUrl, cancelToken).ConfigureAwait(false);
            var body = await httpResponse.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                response.Data.EndpointReachable = false;
                response.Data.PluginActive = false;
                response.Data.ErrorMessage = httpResponse.StatusCode == System.Net.HttpStatusCode.NotFound
                    ? "Wolt probe endpoint not found (plugin may not be installed)."
                    : $"Probe failed (HTTP {(int)httpResponse.StatusCode}).";
                return response;
            }

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("wolt", out var woltEl) &&
                (woltEl.ValueKind == JsonValueKind.True || woltEl.ValueKind == JsonValueKind.False))
            {
                response.Data.EndpointReachable = true;
                response.Data.PluginActive = woltEl.GetBoolean();
                return response;
            }

            response.Data.EndpointReachable = false;
            response.Data.ErrorMessage = "Unexpected probe response (missing wolt field).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Wolt plugin probe failed for site {SiteId}", siteId);
            response.Data.EndpointReachable = false;
            response.Data.ErrorMessage = ex.Message;
        }

        return response;
    }

    public async Task<IApiResponse<WoltDispatchRes>> DispatchOrderAsync(int orderId, CancellationToken cancelToken)
    {
        var response = new ApiResponse<WoltDispatchRes> { Data = new WoltDispatchRes() };

        var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);

        if (!string.Equals(order.DeliveryType, "Shipping", StringComparison.OrdinalIgnoreCase))
        {
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Wolt dispatch is only available for shipping orders.");
        }

        var site = await _siteStorage.GetSiteAsync(order.SiteId, cancelToken).ConfigureAwait(false);
        if (site == null)
            return CreateResponse(response, StatusCode.ItemNotFound, "Site not found.");

        if (string.IsNullOrWhiteSpace(site.WooCommerceUrl))
        {
            return CreateResponse(response, StatusCode.InvalidRequest,
                "WooCommerce site URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(site.WoltDispatchToken))
        {
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Wolt dispatch token is not configured. Set it in Integrations → WOLT settings.");
        }

        if (site.WoltEnabled != true)
        {
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Wolt integration is not enabled for this site.");
        }

        var wooOrderKey = ResolveWooCommerceOrderKey(order);
        if (wooOrderKey == null)
        {
            return CreateResponse(response, StatusCode.InvalidRequest,
                "Order has no WooCommerce order id (externalOrderId).");
        }

        var dispatchUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/ocws-wolt/v1/dispatch";
        object body = wooOrderKey.Value.UseNumericId
            ? new { orderId = wooOrderKey.Value.NumericId }
            : new { orderNumber = wooOrderKey.Value.OrderNumber! };

        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = DispatchHttpTimeout;
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", site.WoltDispatchToken.Trim());

        var json = JsonSerializer.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage httpResponse;
        string responseText;
        try
        {
            httpResponse = await httpClient.PostAsync(dispatchUrl, content, cancelToken).ConfigureAwait(false);
            responseText = await httpResponse.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wolt dispatch HTTP failed for order {OrderId}", orderId);
            response.Data.Success = false;
            response.Data.ErrorCode = "wolt_http_failed";
            response.Data.ErrorMessage = ex.Message;
            return CreateResponse(response, StatusCode.GeneralError, "Wolt dispatch request failed.");
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var (code, message) = ParseWoltError(responseText, (int)httpResponse.StatusCode);
            _logger.LogWarning(
                "Wolt dispatch rejected for order {OrderId}: HTTP {Status} {Body}",
                orderId, (int)httpResponse.StatusCode, TruncateForLog(responseText));
            response.Data.Success = false;
            response.Data.ErrorCode = code;
            response.Data.ErrorMessage = message;
            response.Description = message;
            // HTTP to our client is OK — the WP/Wolt business call failed; UI reads data.success / errorMessage.
            response.StatusCode = StatusCode.Ok;
            return response;
        }

        WoltDispatchApiPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WoltDispatchApiPayload>(responseText, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wolt dispatch response parse failed for order {OrderId}: {Body}", orderId, TruncateForLog(responseText));
            return CreateResponse(response, StatusCode.GeneralError, "Invalid Wolt dispatch response.");
        }

        if (payload?.Order == null)
        {
            return CreateResponse(response, StatusCode.GeneralError, "Wolt dispatch response missing order data.");
        }

        var dispatchedAt = DateTime.UtcNow;
        var trackingUrl = payload.Order.Tracking?.Url?.Trim();
        var trackingId = payload.Order.Tracking?.Id?.Trim();
        var woltStatus = payload.Order.WoltStatus?.Trim();
        var deliveryId = payload.Order.DeliveryId?.Trim();

        var updated = await _orderStorage.UpdateOrderAsync(orderId, o =>
        {
            o.WoltTrackingUrl = string.IsNullOrEmpty(trackingUrl) ? o.WoltTrackingUrl : trackingUrl;
            o.WoltTrackingId = string.IsNullOrEmpty(trackingId) ? o.WoltTrackingId : trackingId;
            o.WoltStatus = string.IsNullOrEmpty(woltStatus) ? o.WoltStatus : woltStatus;
            o.WoltDeliveryId = string.IsNullOrEmpty(deliveryId) ? o.WoltDeliveryId : deliveryId;
            o.WoltDispatchedAt = dispatchedAt;
            o.WoltDeliveryJson = responseText;
        }, cancelToken).ConfigureAwait(false);

        var loaded = updated != null
            ? await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false)
            : null;

        response.Data.Success = true;
        response.Data.Created = payload.Created;
        response.Data.WoltTrackingUrl = trackingUrl;
        response.Data.WoltTrackingId = trackingId;
        response.Data.WoltStatus = woltStatus;
        response.Data.WoltDeliveryId = deliveryId;
        response.Data.WoltDispatchedAt = dispatchedAt;
        response.Data.Order = loaded != null ? _mapper.Map<OrderRes>(loaded) : null;
        return response;
    }

    private static WooOrderKey? ResolveWooCommerceOrderKey(Order order)
    {
        var external = order.ExternalOrderId?.Trim();
        if (!string.IsNullOrEmpty(external))
        {
            var digits = external.TrimStart('#').Trim();
            if (int.TryParse(digits, out var wooId) && wooId > 0)
                return new WooOrderKey(true, wooId, digits);
            if (!string.IsNullOrEmpty(digits))
                return new WooOrderKey(false, 0, digits);
        }

        var orderNumber = order.OrderNumber?.Trim();
        if (!string.IsNullOrEmpty(orderNumber))
        {
            if (int.TryParse(orderNumber, out var numId) && numId > 0)
                return new WooOrderKey(true, numId, orderNumber);
            return new WooOrderKey(false, 0, orderNumber);
        }

        return null;
    }

    private static (string? Code, string? Message) ParseWoltError(string body, int httpStatus)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var codeEl))
            {
                var code = codeEl.GetString();
                var msg = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                return (code, msg ?? $"Wolt dispatch failed (HTTP {httpStatus}).");
            }
            if (root.TryGetProperty("error", out var errEl))
            {
                var code = errEl.GetString();
                var msg = root.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                return (code, msg ?? $"Wolt dispatch failed (HTTP {httpStatus}).");
            }
        }
        catch
        {
            // ignore parse errors
        }

        return ("wolt_dispatch_failed", string.IsNullOrWhiteSpace(body)
            ? $"Wolt dispatch failed (HTTP {httpStatus})."
            : body.Length > 500 ? body[..500] : body);
    }

    private static string TruncateForLog(string? text, int max = 800) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly record struct WooOrderKey(bool UseNumericId, int NumericId, string? OrderNumber);

    private sealed class WoltDispatchApiPayload
    {
        public bool Success { get; set; }
        public bool Created { get; set; }
        public WoltDispatchApiOrder? Order { get; set; }
    }

    private sealed class WoltDispatchApiOrder
    {
        public string? WoltStatus { get; set; }
        public string? DeliveryId { get; set; }
        public WoltDispatchApiTracking? Tracking { get; set; }
    }

    private sealed class WoltDispatchApiTracking
    {
        public string? Url { get; set; }
        public string? Id { get; set; }
    }
}
