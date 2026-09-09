using George.DB;

namespace George.Services.Delivery;

/// <summary>Result of a provider create-task call.</summary>
public sealed record DeliveryCreateResult(
    bool Success,
    string? ExternalTaskId,
    string? TrackingLink,
    string? ErrorMessage);

/// <summary>Result of a provider update-task call.</summary>
public sealed record DeliveryUpdateResult(bool Success, string? ErrorMessage);

/// <summary>
/// One courier company behind the delivery-provider abstraction. Implementations are stateless,
/// resolved from DI by <see cref="DeliveryDispatchService"/> via <see cref="ProviderKey"/>.
/// </summary>
public interface IDeliveryProvider
{
    /// <summary>Stable key stored on configs and dispatch rows (e.g. "lionwheel").</summary>
    string ProviderKey { get; }

    /// <summary>Create a delivery task for the order. Must not throw - return a failed result instead.</summary>
    Task<DeliveryCreateResult> CreateTaskAsync(Order order, DeliveryProviderConfig config, CancellationToken cancelToken);

    /// <summary>Cancel a previously created task. Returns false (without throwing) when the provider rejects.</summary>
    Task<bool> CancelTaskAsync(string externalTaskId, DeliveryProviderConfig config, CancellationToken cancelToken);

    /// <summary>
    /// Push the order's current delivery details (supply date, address, recipient, notes) onto an
    /// existing task. Must not throw - return a failed result instead.
    /// </summary>
    Task<DeliveryUpdateResult> UpdateTaskAsync(Order order, string externalTaskId, DeliveryProviderConfig config, CancellationToken cancelToken);

    /// <summary>
    /// Parse the provider's incoming status webhook payload; null when the payload carries no
    /// usable (taskId, courierStatus) pair.
    /// </summary>
    (string TaskId, string CourierStatus)? ParseWebhookStatus(string payloadJson);
}
