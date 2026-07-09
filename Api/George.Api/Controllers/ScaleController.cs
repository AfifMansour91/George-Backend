using George.Api.Core;
using George.Api.Services;
using George.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

/// <summary>
/// Ingest endpoint for the branch ScaleAgent. The agent reads the SHEKEL scale over RS232 and POSTs
/// weight readings here; the backend relays them to the picking page over SignalR (<see cref="Hubs.ScaleHub"/>).
/// Auth mirrors PrintJob: the local branch agent key (X-Print-Agent-Key) or a Bearer token.
/// </summary>
[Route("[controller]", Name = "Scale")]
[ApiController]
[Authorize(AuthenticationSchemes = "Bearer," + PrintAgentApiKeyAuthenticationHandler.SchemeName)]
public class ScaleController : GeorgeControllerBase
{
    private readonly IScaleRealtimeNotifier _notifier;

    public ScaleController(IScaleRealtimeNotifier notifier, ILogger<ScaleController> logger)
        : base(logger)
    {
        _notifier = notifier;
    }

    /// <summary>ScaleAgent posts a (throttled / on-change) weight reading for a branch.</summary>
    [HttpPost("Reading")]
    [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> PostReadingAsync([FromBody] ScaleReadingReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _notifier.PublishReadingAsync(req, cancelToken));
    }
}
