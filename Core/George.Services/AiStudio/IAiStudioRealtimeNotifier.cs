using George.Services.Response;

namespace George.Services.AiStudio;

/// <summary>Pushes job progress to the shop's open pages (SignalR in the API host; a no-op elsewhere). Pattern: IScaleRealtimeNotifier.</summary>
public interface IAiStudioRealtimeNotifier
{
    Task JobUpdatedAsync(int accountId, AiJobRes job, CancellationToken cancelToken);
}

public sealed class NullAiStudioRealtimeNotifier : IAiStudioRealtimeNotifier
{
    public Task JobUpdatedAsync(int accountId, AiJobRes job, CancellationToken cancelToken) => Task.CompletedTask;
}
