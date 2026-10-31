using George.Api.Hubs;
using George.Services.AiStudio;
using George.Services.Response;
using Microsoft.AspNetCore.SignalR;

namespace George.Api.Services;

/// <summary>SignalR implementation of <see cref="IAiStudioRealtimeNotifier"/>: one <c>AiJobUpdated</c> per job change to the account group.</summary>
public sealed class AiStudioRealtimeNotifier : IAiStudioRealtimeNotifier
{
    private readonly IHubContext<AiStudioHub> _hub;
    private readonly ILogger<AiStudioRealtimeNotifier> _logger;

    public AiStudioRealtimeNotifier(IHubContext<AiStudioHub> hub, ILogger<AiStudioRealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task JobUpdatedAsync(int accountId, AiJobRes job, CancellationToken cancelToken)
    {
        try
        {
            await _hub.Clients.Group(AiStudioHub.AccountGroup(accountId)).SendAsync(AiStudioHub.JobUpdatedEvent, job, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AiStudio realtime push failed for job {JobId}", job.Id);
        }
    }
}
