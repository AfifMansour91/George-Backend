using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

[Route("[controller]", Name = "Dashboard")]
[ApiController]
public class DashboardController : GeorgeControllerBase, IAuthUserProvider
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService, ILogger<DashboardController> logger)
        : base(logger)
    {
        _dashboardService = dashboardService;
    }

    /// <summary>Store operations dashboard - KPIs, active orders, forward projection, insights.</summary>
    [HttpGet("Summary")]
    [ProducesResponseType(typeof(IApiResponse<DashboardSummaryRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSummaryAsync(
        [FromQuery] int siteId,
        [FromQuery] string period = "today",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int? accountId = null,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _dashboardService.GetSummaryAsync(siteId, period, from, to, accountId, cancelToken));
    }

  /// <summary>Live activity feed for dashboard.</summary>
    [HttpGet("LiveActivity")]
    [ProducesResponseType(typeof(IApiResponse<List<LiveActivityEventRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetLiveActivityAsync(
        [FromQuery] int siteId,
        [FromQuery] DateTime? since = null,
        [FromQuery] int limit = 20,
        [FromQuery] int? accountId = null,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _dashboardService.GetLiveActivityAsync(siteId, since, limit, accountId, cancelToken));
    }

    /// <summary>Top products needed for delivery/pickup in the selected window.</summary>
    [HttpGet("DeliveryProducts")]
    [ProducesResponseType(typeof(IApiResponse<List<DeliveryProductRowRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetDeliveryProductsAsync(
        [FromQuery] int siteId,
        [FromQuery] string window = "today",
        [FromQuery] int limit = 5,
        [FromQuery] int? accountId = null,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _dashboardService.GetDeliveryProductsAsync(siteId, window, limit, accountId, cancelToken));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public void SetAuthUser()
    {
        SetAuthUser(_dashboardService);
    }
}
