using System.Text.Json;
using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Services;

public partial class OrderService
{
    /// <summary>
    /// Record a single order action to <see cref="IntegrationLog"/> (EntityType="order") for the admin
    /// sync/activity-logs screen. Covers every order operation across every source (website / phone /
    /// kiosk / manual). Best-effort: a logging failure must never break the operation it logs.
    /// </summary>
    /// <param name="operation">e.g. create | update | cancel | add_items | remove_item | picking | payment | reminder.</param>
    /// <param name="direction">internal (in-app) | inbound (store → George) | outbound (George → store).</param>
    /// <remarks>
    /// Non-blocking: the log row is built synchronously here (capturing the order's state at call time),
    /// then enqueued to <see cref="IIntegrationLogQueue"/> for the background batch writer -
    /// the order operation does NOT wait for the DB write. Returns a completed task so existing
    /// <c>await</c> call sites stay valid at zero cost. <paramref name="cancelToken"/> is intentionally
    /// not used for the background write (the request token may be cancelled once the response returns).
    /// </remarks>
    private Task LogOrderEventAsync(
        IntegrationLogOperation operation,
        IntegrationLogDirection direction,
        int siteId,
        int? orderId,
        string? externalOrderId,
        bool success,
        string? error = null,
        string? requestJson = null,
        int? httpStatus = null,
        CancellationToken cancelToken = default)
    {
        var row = new IntegrationLog
        {
            SiteId = siteId,
            EntityType = IntegrationLogEntityType.Order.ToWire(),
            EntityId = orderId,
            ExternalId = string.IsNullOrWhiteSpace(externalOrderId) ? null : externalOrderId.Trim(),
            Direction = direction.ToWire(),
            Operation = operation.ToWire(),
            Level = (success ? IntegrationLogLevel.Info : IntegrationLogLevel.Error).ToWire(),
            HttpStatus = httpStatus,
            Success = success,
            RequestJson = TruncateLog(requestJson, 20000),
            Error = TruncateLog(error, 1000),
            CreatedAtUtc = DateTime.UtcNow,
        };
        _integrationLogQueue.TryEnqueue(row);
        return Task.CompletedTask;
    }

    /// <summary>Compact human/JSON snapshot of an order's key fields, stored in the log row for context.</summary>
    private static string OrderLogSnapshot(Order order)
    {
        try
        {
            return JsonSerializer.Serialize(new
            {
                source = order.Source,
                status = order.Status,
                total = order.Total,
                itemCount = order.OrderItem?.Count(i => !i.IsDeleted) ?? 0,
                customerPhone = order.CustomerPhone,
                deliveryType = order.DeliveryType,
            });
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? TruncateLog(string? value, int maxChars) =>
        string.IsNullOrEmpty(value) || value!.Length <= maxChars ? value : value.Substring(0, maxChars);
}
