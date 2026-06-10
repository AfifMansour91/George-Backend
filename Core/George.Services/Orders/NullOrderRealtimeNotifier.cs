using George.Services.Response;

namespace George.Services.Orders;

/// <summary>No-op fallback when SignalR is not registered.</summary>
public class NullOrderRealtimeNotifier : IOrderRealtimeNotifier
{
    public Task NotifyNewOrderAsync(OrderRes order, CancellationToken cancelToken = default) =>
        Task.CompletedTask;
}
