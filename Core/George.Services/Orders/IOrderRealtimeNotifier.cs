using George.Services.Response;

namespace George.Services.Orders;

public interface IOrderRealtimeNotifier
{
    Task NotifyNewOrderAsync(OrderRes order, CancellationToken cancelToken = default);
}
