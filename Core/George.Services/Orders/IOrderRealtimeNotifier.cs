using George.Services.Response;

namespace George.Services.Orders;

public interface IOrderRealtimeNotifier
{
    Task NotifyNewOrderAsync(OrderRes order, CancellationToken cancelToken = default);
    /// <summary>Push a "no invoice for a charged order" alert to the site's open sessions.</summary>
    Task NotifyMissingInvoiceAsync(MissingInvoiceAlertEvent alert, CancellationToken cancelToken = default);
}
