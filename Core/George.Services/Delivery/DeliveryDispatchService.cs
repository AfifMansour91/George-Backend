using System.Text.Json;
using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Services.Delivery;

/// <summary>
/// Delivery-provider orchestrator: pushes shipping orders to every enabled provider whose
/// configured trigger status is reached, cancels courier tasks when orders are cancelled,
/// serves manual retry, and applies incoming provider status webhooks.
/// All order-flow entry points are fire-safe - a provider failure is recorded, never thrown.
/// </summary>
public class DeliveryDispatchService : ServiceBase
{
    public const string StatusDispatched = "dispatched";
    public const string StatusFailed = "failed";
    public const string StatusCancelled = "cancelled";

    private readonly OrderStorage _orderStorage;
    private readonly DeliveryDispatchStorage _dispatchStorage;
    private readonly IReadOnlyDictionary<string, IDeliveryProvider> _providers;

    public DeliveryDispatchService(
        ILogger<DeliveryDispatchService> logger,
        IMapper mapper,
        CacheManager cache,
        OrderStorage orderStorage,
        DeliveryDispatchStorage dispatchStorage,
        IEnumerable<IDeliveryProvider> providers)
        : base(logger, mapper, cache)
    {
        _orderStorage = orderStorage;
        _dispatchStorage = dispatchStorage;
        _providers = providers.ToDictionary(p => p.ProviderKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Rank on the New → InTreatment → Ready ladder (0 = not on the ladder). Dispatch fires when
    /// the order reaches a rank at or beyond the trigger rank, so an order that jumps straight
    /// from New to Ready still dispatches when the trigger is InTreatment.
    /// </summary>
    private static int StatusRank(string? status)
    {
        var s = status?.Trim();
        if (string.Equals(s, "New", StringComparison.OrdinalIgnoreCase)) return 1;
        if (string.Equals(s, "InTreatment", StringComparison.OrdinalIgnoreCase)) return 2;
        if (string.Equals(s, "Ready", StringComparison.OrdinalIgnoreCase)) return 3;
        return 0;
    }

    /// <summary>Status-driven dispatch entry point (order create + status change). Never throws.</summary>
    public async Task TryDispatchOnStatusAsync(int orderId, string? newStatus, CancellationToken cancelToken = default)
    {
        try
        {
            var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null) return;
            if (!string.Equals(order.DeliveryType?.Trim(), "Shipping", StringComparison.OrdinalIgnoreCase)) return;
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)) return;

            var configs = await _dispatchStorage.GetConfigsForSiteAsync(order.SiteId, cancelToken).ConfigureAwait(false);
            var statusRank = StatusRank(newStatus ?? order.Status);
            foreach (var config in configs)
            {
                if (!config.Enabled) continue;
                var triggerRank = StatusRank(config.TriggerStatus);
                if (triggerRank == 0 || statusRank < triggerRank) continue;
                await TryDispatchToProviderAsync(order, config, cancelToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delivery dispatch-on-status failed for order {OrderId}", orderId);
        }
    }

    /// <summary>Manual retry from the order UI. Returns the dispatch outcome for display.</summary>
    public async Task<IApiResponse<OrderDeliveryDispatchRes>> RetryDispatchAsync(
        int orderId,
        string providerKey,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<OrderDeliveryDispatchRes>();
        var order = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
        if (order == null)
            return CreateResponse(response, StatusCode.ItemNotFound);
        if (!string.Equals(order.DeliveryType?.Trim(), "Shipping", StringComparison.OrdinalIgnoreCase))
            return CreateResponse(response, StatusCode.InvalidRequest, "Delivery dispatch is only available for shipping orders.");
        if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            return CreateResponse(response, StatusCode.InvalidRequest, "Order is cancelled.");

        var config = await _dispatchStorage.GetConfigAsync(order.SiteId, providerKey, cancelToken).ConfigureAwait(false);
        if (config == null || !config.Enabled)
            return CreateResponse(response, StatusCode.InvalidRequest, "Delivery provider is not enabled for this site.");

        var row = await TryDispatchToProviderAsync(order, config, cancelToken).ConfigureAwait(false);
        if (row == null)
            return CreateResponse(response, StatusCode.InvalidRequest, "Order was already dispatched to this provider.");

        response.Data = MapDispatch(row);
        return response;
    }

    /// <summary>
    /// Cancels all provider tasks of a cancelled order. Never throws.
    /// Deliberately does NOT load the order - order cancel soft-deletes it, and the standard
    /// order lookup filters IsDeleted, which would silently skip the courier cancel. Everything
    /// needed (SiteId, ProviderKey, TaskId) lives on the dispatch rows.
    /// </summary>
    public async Task TryCancelForOrderAsync(int orderId, CancellationToken cancelToken = default)
    {
        try
        {
            var dispatches = await _dispatchStorage.GetDispatchesForOrderAsync(orderId, cancelToken).ConfigureAwait(false);
            foreach (var d in dispatches)
            {
                if (string.IsNullOrWhiteSpace(d.ExternalTaskId)) continue;
                if (string.Equals(d.Status, StatusCancelled, StringComparison.OrdinalIgnoreCase)) continue;
                if (!_providers.TryGetValue(d.ProviderKey, out var provider)) continue;

                var config = await _dispatchStorage.GetConfigAsync(d.SiteId, d.ProviderKey, cancelToken).ConfigureAwait(false);
                if (config == null) continue;

                var ok = await provider.CancelTaskAsync(d.ExternalTaskId!, config, cancelToken).ConfigureAwait(false);
                if (!ok)
                {
                    _logger.LogWarning(
                        "Delivery provider {Provider} rejected cancel for order {OrderId} task {TaskId}",
                        d.ProviderKey, orderId, d.ExternalTaskId);
                    continue;
                }

                var row = await _dispatchStorage.UpsertDispatchAsync(orderId, d.SiteId, d.ProviderKey, x =>
                {
                    x.Status = StatusCancelled;
                }, cancelToken).ConfigureAwait(false);
                await DenormalizeOntoOrderAsync(orderId, row, cancelToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Delivery task {TaskId} ({Provider}) cancelled for order {OrderId}", d.ExternalTaskId, d.ProviderKey, orderId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delivery cancel-for-order failed for order {OrderId}", orderId);
        }
    }

    /// <summary>
    /// Applies an incoming provider status webhook. Secret must match the site's provider config.
    /// </summary>
    public async Task<IApiResponse<bool>> ApplyWebhookAsync(
        string providerKey,
        int siteId,
        string? secret,
        string payloadJson,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<bool> { Data = false };
        if (!_providers.TryGetValue(providerKey, out var provider))
            return CreateResponse(response, StatusCode.ItemNotFound, "Unknown delivery provider.");

        var config = await _dispatchStorage.GetConfigAsync(siteId, providerKey, cancelToken).ConfigureAwait(false);
        if (config == null || string.IsNullOrWhiteSpace(config.WebhookSecret) ||
            !string.Equals(config.WebhookSecret, secret?.Trim(), StringComparison.Ordinal))
            return CreateResponse(response, StatusCode.InvalidRequest, "Invalid webhook secret.");

        var parsed = provider.ParseWebhookStatus(payloadJson);
        if (parsed == null)
        {
            // Unusable payload is not an error towards the provider - acknowledge so they don't retry forever.
            _logger.LogWarning("Delivery webhook payload not parseable (provider {Provider}, site {SiteId})", providerKey, siteId);
            response.Data = false;
            return response;
        }

        var (taskId, courierStatus) = parsed.Value;
        var dispatch = await _dispatchStorage.GetDispatchByTaskAsync(providerKey, taskId, cancelToken).ConfigureAwait(false);
        if (dispatch == null)
        {
            _logger.LogWarning(
                "Delivery webhook for unknown task {TaskId} (provider {Provider}, site {SiteId})", taskId, providerKey, siteId);
            response.Data = false;
            return response;
        }

        var row = await _dispatchStorage.UpsertDispatchAsync(dispatch.OrderId, dispatch.SiteId, providerKey, x =>
        {
            x.CourierStatus = courierStatus;
            x.CourierStatusUpdatedAt = DateTime.UtcNow;
            // Courier lifecycle overrides our local "dispatched" once the courier reports movement.
            if (!string.Equals(x.Status, StatusCancelled, StringComparison.OrdinalIgnoreCase))
                x.Status = courierStatus;
        }, cancelToken).ConfigureAwait(false);
        await DenormalizeOntoOrderAsync(dispatch.OrderId, row, cancelToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Delivery webhook applied: order {OrderId} task {TaskId} ({Provider}) → {Status}",
            dispatch.OrderId, taskId, providerKey, courierStatus);
        response.Data = true;
        return response;
    }

    /// <summary>Dispatch detail rows for one order (order page display).</summary>
    public async Task<IApiResponse<List<OrderDeliveryDispatchRes>>> GetDispatchesForOrderAsync(
        int orderId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<OrderDeliveryDispatchRes>>();
        var rows = await _dispatchStorage.GetDispatchesForOrderAsync(orderId, cancelToken).ConfigureAwait(false);
        response.Data = rows.Select(MapDispatch).ToList();
        return response;
    }

    /// <summary>Provider configs for the site's Integrations page. API keys are masked (write-only).</summary>
    public async Task<IApiResponse<List<DeliveryProviderConfigRes>>> GetSiteProviderConfigsAsync(
        int siteId, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<List<DeliveryProviderConfigRes>>();
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
        var rows = await _dispatchStorage.GetConfigsForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        response.Data = rows.Select(MapConfig).ToList();
        return response;
    }

    /// <summary>
    /// Create-or-update a provider config. An empty <see cref="DeliveryProviderConfigReq.ApiKey"/>
    /// keeps the stored key (write-only pattern, same as SMS settings).
    /// </summary>
    public async Task<IApiResponse<DeliveryProviderConfigRes>> UpsertSiteProviderConfigAsync(
        int siteId,
        string providerKey,
        DeliveryProviderConfigReq req,
        CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<DeliveryProviderConfigRes>();
        if (siteId <= 0)
            return CreateResponse(response, StatusCode.InvalidRequest, "SiteId is required.");
        var key = providerKey?.Trim().ToLowerInvariant() ?? "";
        if (!_providers.ContainsKey(key))
            return CreateResponse(response, StatusCode.InvalidRequest, $"Unknown delivery provider '{providerKey}'.");

        var row = await _dispatchStorage.UpsertConfigAsync(siteId, key, c =>
        {
            if (req.Enabled.HasValue) c.Enabled = req.Enabled.Value;
            if (!string.IsNullOrWhiteSpace(req.ApiKey)) c.ApiKey = req.ApiKey.Trim();
            if (req.TriggerStatus != null) c.TriggerStatus = NullIfWhiteSpace(req.TriggerStatus);
            if (req.PickupCity != null) c.PickupCity = NullIfWhiteSpace(req.PickupCity);
            if (req.PickupStreet != null) c.PickupStreet = NullIfWhiteSpace(req.PickupStreet);
            if (req.PickupNumber != null) c.PickupNumber = NullIfWhiteSpace(req.PickupNumber);
            if (req.PickupName != null) c.PickupName = NullIfWhiteSpace(req.PickupName);
            if (req.PickupPhone != null) c.PickupPhone = NullIfWhiteSpace(req.PickupPhone);
            if (req.Settings != null)
            {
                // Provider-specific extras (e.g. LionWheel company_id) - merge per key, empty value removes.
                var current = ParseSettings(c.SettingsJson);
                foreach (var kv in req.Settings)
                {
                    if (string.IsNullOrWhiteSpace(kv.Value)) current.Remove(kv.Key);
                    else current[kv.Key] = kv.Value.Trim();
                }
                c.SettingsJson = current.Count > 0 ? JsonSerializer.Serialize(current) : null;
            }
        }, cancelToken).ConfigureAwait(false);

        if (row.Enabled && string.IsNullOrWhiteSpace(row.ApiKey))
            return CreateResponse(response, StatusCode.InvalidRequest, "API key is required to enable the provider.");

        response.Data = MapConfig(row);
        return response;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Parses SettingsJson into a flat string map (invalid/empty JSON → empty map).</summary>
    public static Dictionary<string, string> ParseSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
            return parsed != null
                ? new Dictionary<string, string>(parsed, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static DeliveryProviderConfigRes MapConfig(DeliveryProviderConfig c) => new()
    {
        ProviderKey = c.ProviderKey,
        Enabled = c.Enabled,
        HasApiKey = !string.IsNullOrWhiteSpace(c.ApiKey),
        TriggerStatus = c.TriggerStatus,
        PickupCity = c.PickupCity,
        PickupStreet = c.PickupStreet,
        PickupNumber = c.PickupNumber,
        PickupName = c.PickupName,
        PickupPhone = c.PickupPhone,
        Settings = ParseSettings(c.SettingsJson),
        // Relative path; the SPA prefixes the API origin and shows it for copy into the provider console.
        WebhookPath = $"/Webhooks/Delivery/{c.ProviderKey}?siteId={c.SiteId}&secret={c.WebhookSecret}",
    };

    /// <summary>
    /// One dispatch attempt to one provider. Returns null when skipped (already successfully
    /// dispatched); otherwise the recorded dispatch row (success or failure).
    /// </summary>
    private async Task<OrderDeliveryDispatch?> TryDispatchToProviderAsync(
        Order order,
        DeliveryProviderConfig config,
        CancellationToken cancelToken)
    {
        if (!_providers.TryGetValue(config.ProviderKey, out var provider))
        {
            _logger.LogWarning("No IDeliveryProvider registered for key {Provider}", config.ProviderKey);
            return null;
        }

        var existing = await _dispatchStorage.GetDispatchAsync(order.Id, config.ProviderKey, cancelToken).ConfigureAwait(false);
        // Skip when a task already exists and was not cancelled - retry is only for failed attempts.
        if (existing != null && !string.IsNullOrWhiteSpace(existing.ExternalTaskId) &&
            !string.Equals(existing.Status, StatusCancelled, StringComparison.OrdinalIgnoreCase))
            return null;

        var result = await provider.CreateTaskAsync(order, config, cancelToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var row = await _dispatchStorage.UpsertDispatchAsync(order.Id, order.SiteId, config.ProviderKey, x =>
        {
            x.AttemptCount += 1;
            x.LastAttemptAt = now;
            if (result.Success)
            {
                x.ExternalTaskId = result.ExternalTaskId;
                x.TrackingLink = result.TrackingLink;
                x.Status = StatusDispatched;
                x.ErrorMessage = null;
                x.DispatchedAt = now;
            }
            else
            {
                x.Status = StatusFailed;
                x.ErrorMessage = result.ErrorMessage;
            }
        }, cancelToken).ConfigureAwait(false);

        await DenormalizeOntoOrderAsync(order.Id, row, cancelToken).ConfigureAwait(false);

        if (result.Success)
            _logger.LogInformation(
                "Delivery task {TaskId} ({Provider}) created for order {OrderId}", result.ExternalTaskId, config.ProviderKey, order.Id);
        else
            _logger.LogWarning(
                "Delivery dispatch failed ({Provider}) for order {OrderId}: {Error}", config.ProviderKey, order.Id, result.ErrorMessage);
        return row;
    }

    private async Task DenormalizeOntoOrderAsync(int orderId, OrderDeliveryDispatch row, CancellationToken cancelToken)
    {
        await _orderStorage.UpdateOrderAsync(orderId, o =>
        {
            o.DeliveryProviderKey = row.ProviderKey;
            o.DeliveryProviderTaskId = row.ExternalTaskId;
            o.DeliveryProviderTrackingLink = row.TrackingLink;
            o.DeliveryProviderStatus = row.Status;
            o.DeliveryProviderError = row.ErrorMessage;
            o.DeliveryProviderDispatchedAt = row.DispatchedAt;
        }, cancelToken).ConfigureAwait(false);
    }

    private static OrderDeliveryDispatchRes MapDispatch(OrderDeliveryDispatch d) => new()
    {
        ProviderKey = d.ProviderKey,
        ExternalTaskId = d.ExternalTaskId,
        TrackingLink = d.TrackingLink,
        Status = d.Status,
        ErrorMessage = d.ErrorMessage,
        AttemptCount = d.AttemptCount,
        DispatchedAt = d.DispatchedAt,
        LastAttemptAt = d.LastAttemptAt,
        CourierStatus = d.CourierStatus,
        CourierStatusUpdatedAt = d.CourierStatusUpdatedAt,
    };
}

/// <summary>Provider config API shape (Integrations page). ApiKey is write-only - only HasApiKey is returned.</summary>
public class DeliveryProviderConfigRes
{
    public string ProviderKey { get; set; } = "";
    public bool Enabled { get; set; }
    public bool HasApiKey { get; set; }
    public string? TriggerStatus { get; set; }
    public string? PickupCity { get; set; }
    public string? PickupStreet { get; set; }
    public string? PickupNumber { get; set; }
    public string? PickupName { get; set; }
    public string? PickupPhone { get; set; }
    /// <summary>Provider-specific extras (e.g. LionWheel company_id).</summary>
    public Dictionary<string, string> Settings { get; set; } = new();
    /// <summary>Relative webhook path (incl. site + secret) to paste at the provider console.</summary>
    public string WebhookPath { get; set; } = "";
}

/// <summary>Provider config upsert request. Null field = keep existing; empty ApiKey = keep stored key.</summary>
public class DeliveryProviderConfigReq
{
    public bool? Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? TriggerStatus { get; set; }
    public string? PickupCity { get; set; }
    public string? PickupStreet { get; set; }
    public string? PickupNumber { get; set; }
    public string? PickupName { get; set; }
    public string? PickupPhone { get; set; }
    /// <summary>Provider-specific extras to merge (empty value removes the key).</summary>
    public Dictionary<string, string>? Settings { get; set; }
}

/// <summary>API shape of one order-delivery dispatch row.</summary>
public class OrderDeliveryDispatchRes
{
    public string ProviderKey { get; set; } = "";
    public string? ExternalTaskId { get; set; }
    public string? TrackingLink { get; set; }
    public string Status { get; set; } = "";
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public string? CourierStatus { get; set; }
    public DateTime? CourierStatusUpdatedAt { get; set; }
}
