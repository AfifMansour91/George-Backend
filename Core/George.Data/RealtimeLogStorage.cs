using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Data;

public class RealtimeLogStorage : StorageBase
{
    public RealtimeLogStorage(GeorgeDBContext dbContext, ILogger<RealtimeLogStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task AppendHubLogAsync(
        string hubName,
        string eventType,
        string connectionId,
        int? userId = null,
        int? siteId = null,
        int? accountId = null,
        string? feature = null,
        string? detail = null,
        CancellationToken cancelToken = default)
    {
        _dbContext.RealtimeHubLog.Add(new RealtimeHubLog
        {
            HubName = hubName,
            Feature = feature,
            EventType = eventType,
            ConnectionId = connectionId,
            UserId = userId,
            SiteId = siteId,
            AccountId = accountId,
            Detail = detail,
            CreationTime = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    public async Task AppendEventLogAsync(
        string hubName,
        string feature,
        string eventName,
        bool success,
        int? siteId = null,
        int? accountId = null,
        string? entityType = null,
        string? entityId = null,
        string? payloadJson = null,
        string? detail = null,
        CancellationToken cancelToken = default)
    {
        _dbContext.RealtimeEventLog.Add(new RealtimeEventLog
        {
            HubName = hubName,
            Feature = feature,
            EventName = eventName,
            SiteId = siteId,
            AccountId = accountId,
            EntityType = entityType,
            EntityId = entityId,
            PayloadJson = payloadJson,
            Success = success,
            Detail = detail,
            CreationTime = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }
}
