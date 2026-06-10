using System.Text.Json;
using George.Api.Hubs;
using George.Common;
using George.Data;
using George.Services.Orders;
using George.Services.Response;
using Microsoft.AspNetCore.SignalR;

namespace George.Api.Services;

public class SignalROrderRealtimeNotifier : IOrderRealtimeNotifier
{
    private readonly IHubContext<OrdersHub> _hub;
    private readonly RealtimeLogStorage _logStorage;
    private readonly ILogger<SignalROrderRealtimeNotifier> _logger;

    public SignalROrderRealtimeNotifier(
        IHubContext<OrdersHub> hub,
        RealtimeLogStorage logStorage,
        ILogger<SignalROrderRealtimeNotifier> logger)
    {
        _hub = hub;
        _logStorage = logStorage;
        _logger = logger;
    }

    public async Task NotifyNewOrderAsync(OrderRes order, CancellationToken cancelToken = default)
    {
        if (!SysConfig.Data.OrdersRealtimeEnabled)
            return;

        if (order == null || order.Id <= 0 || order.SiteId <= 0)
            return;

        if (!string.Equals(order.Status, "New", StringComparison.OrdinalIgnoreCase))
            return;

        var payload = new NewOrderCreatedEvent
        {
            OrderId = order.Id,
            OrderNumber = order.OrderNumber ?? "",
            AccountId = order.AccountId,
            SiteId = order.SiteId,
            Source = order.Source ?? "",
            Status = order.Status ?? "New",
            CreationTime = order.CreationTime,
        };

        string? payloadJson = null;
        try
        {
            payloadJson = JsonSerializer.Serialize(payload);
        }
        catch
        {
            // optional
        }

        try
        {
            await _hub.Clients
                .Group(OrdersHub.SiteGroup(order.SiteId))
                .SendAsync(RealtimeEventNames.NewOrderCreated, payload, cancelToken)
                .ConfigureAwait(false);

            await AppendEventLogAsync(order, payloadJson, success: true, detail: null, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push NewOrderCreated for order {OrderId}", order.Id);
            await AppendEventLogAsync(order, payloadJson, success: false, detail: ex.Message, cancelToken).ConfigureAwait(false);
        }
    }

    private async Task AppendEventLogAsync(
        OrderRes order,
        string? payloadJson,
        bool success,
        string? detail,
        CancellationToken cancelToken)
    {
        try
        {
            await _logStorage.AppendEventLogAsync(
                RealtimeHubNames.Orders,
                RealtimeFeatures.NewOrder,
                RealtimeEventNames.NewOrderCreated,
                success,
                siteId: order.SiteId,
                accountId: order.AccountId,
                entityType: RealtimeEntityTypes.Order,
                entityId: order.Id.ToString(),
                payloadJson: payloadJson,
                detail: detail,
                cancelToken: cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RealtimeEventLog write failed");
        }
    }
}
