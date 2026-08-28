using George.Api.Hubs;
using George.Common;
using Microsoft.AspNetCore.SignalR;

namespace George.Api.Services;

/// <summary>Reading POSTed by the branch ScaleAgent (POST /Scale/Reading).</summary>
public class ScaleReadingReq
{
    public int SiteId { get; set; }
    /// <summary>Parsed weight in kg. Null for over/under/negative frames.</summary>
    public decimal? WeightKg { get; set; }
    /// <summary>Parser status: Stable | Unstable | Zero | Negative | Over | Under | Invalid.</summary>
    public string? Status { get; set; }
    /// <summary>Convenience flag (parser IsStableWeight) - the picking UI auto-captures only when true.</summary>
    public bool Stable { get; set; }
    /// <summary>Optional agent label (machine name).</summary>
    public string? AgentId { get; set; }
    /// <summary>Agent-side unix-ms timestamp (best-effort ordering; server does not depend on it).</summary>
    public long Ts { get; set; }
}

/// <summary>Payload pushed to picking clients over SignalR (event <c>ScaleWeightChanged</c>).</summary>
public class ScaleWeightEvent
{
    public int SiteId { get; set; }
    public decimal? WeightKg { get; set; }
    public string? Status { get; set; }
    public bool Stable { get; set; }
    public long Ts { get; set; }
}

public interface IScaleRealtimeNotifier
{
    Task<IApiResponse<object>> PublishReadingAsync(ScaleReadingReq req, CancellationToken cancelToken = default);
}

/// <summary>Broadcasts a scale reading to the site group on <see cref="ScaleHub"/>.</summary>
public class ScaleRealtimeNotifier : IScaleRealtimeNotifier
{
    private readonly IHubContext<ScaleHub> _hub;
    private readonly ILogger<ScaleRealtimeNotifier> _logger;

    public ScaleRealtimeNotifier(IHubContext<ScaleHub> hub, ILogger<ScaleRealtimeNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task<IApiResponse<object>> PublishReadingAsync(ScaleReadingReq req, CancellationToken cancelToken = default)
    {
        var response = new ApiResponse<object>();

        if (!SysConfig.Data.ScaleRealtimeEnabled)
            return response; // feature off - accept silently so the agent doesn't error-loop

        if (req == null || req.SiteId <= 0)
        {
            response.StatusCode = StatusCode.InvalidRequest;
            return response;
        }

        var payload = new ScaleWeightEvent
        {
            SiteId = req.SiteId,
            WeightKg = req.WeightKg,
            Status = req.Status,
            Stable = req.Stable,
            Ts = req.Ts,
        };

        try
        {
            await _hub.Clients
                .Group(ScaleHub.SiteGroup(req.SiteId))
                .SendAsync(RealtimeEventNames.ScaleWeightChanged, payload, cancelToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push ScaleWeightChanged for site {SiteId}", req.SiteId);
        }

        return response;
    }
}
