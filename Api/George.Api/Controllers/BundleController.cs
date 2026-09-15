using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

/// <summary>
/// Bundles (מארזים) - BUNDLES_SYNC_SPEC.md §3.2. Every action requires <c>Account.BundlesEnabled</c> on the
/// caller's account (the site's account when a siteId is given); otherwise HTTP 403.
/// The bundle definition itself is saved through <c>POST/PUT /Product</c> (<c>ProductReq.Bundle</c>).
/// </summary>
[Route("[controller]", Name = "Bundle")]
[ApiController]
public class BundleController : GeorgeControllerBase, IAuthUserProvider
{
    private readonly BundleService _bundleService;

    public BundleController(BundleService bundleService, ILogger<BundleController> logger)
        : base(logger)
    {
        _bundleService = bundleService;
    }

    /// <summary>GET /Bundle?siteId=&amp;search=&amp;status=&amp;page=&amp;pageSize= - list rows with per-site Woo sync statuses.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IApiResponse<BundleListRes>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> GetBundlesAsync([FromQuery] BundleListReq req, CancellationToken cancelToken = default)
    {
        if (!await _bundleService.IsBundlesEnabledForCallerAsync(req.SiteId, cancelToken, req.AccountId))
            return BundlesDisabled();
        return await SafeCallWithErrorCatchingAsync(() => _bundleService.ListAsync(req, cancelToken));
    }

    /// <summary>GET /Bundle/{productId}/availability?siteId= - proxies OC Bundles <c>GET /bundles/{wc_id}/availability</c>; 404 when not synced.</summary>
    [HttpGet("{productId:int}/availability")]
    [ProducesResponseType(typeof(IApiResponse<BundleAvailabilityRes>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    public async Task<IActionResult> GetAvailabilityAsync([FromRoute] int productId, [FromQuery] int siteId, CancellationToken cancelToken = default)
    {
        if (!await _bundleService.IsBundlesEnabledForCallerAsync(siteId, cancelToken))
            return BundlesDisabled();
        var response = await _bundleService.GetAvailabilityAsync(productId, siteId, cancelToken);
        if (!response.IsSuccessful && response.StatusCode == Common.StatusCode.ItemNotFound)
            return StatusCode((int)HttpStatusCode.NotFound, response);
        return await SafeCallWithErrorCatchingAsync(() => Task.FromResult(response));
    }

    /// <summary>POST /Bundle/{productId}/sync?siteId= - re-run the product + OC Bundles definition sync for one site.</summary>
    [HttpPost("{productId:int}/sync")]
    [ProducesResponseType(typeof(IApiResponse<BundleSyncRes>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> SyncAsync([FromRoute] int productId, [FromQuery] int siteId, CancellationToken cancelToken = default)
    {
        if (!await _bundleService.IsBundlesEnabledForCallerAsync(siteId, cancelToken))
            return BundlesDisabled();
        return await SafeCallWithErrorCatchingAsync(() => _bundleService.SyncAsync(productId, siteId, cancelToken));
    }

    /// <summary>POST /Bundle/price - server pricing for the UI (new order, picking) - spec §3.2 / §4.</summary>
    [HttpPost("price")]
    [ProducesResponseType(typeof(IApiResponse<BundlePriceRes>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    public async Task<IActionResult> PriceAsync([FromBody] BundlePriceReq req, CancellationToken cancelToken = default)
    {
        if (!await _bundleService.IsBundlesEnabledForCallerAsync(req.SiteId > 0 ? req.SiteId : null, cancelToken))
            return BundlesDisabled();
        return await SafeCallWithErrorCatchingAsync(() => _bundleService.PriceAsync(req, cancelToken));
    }

    private IActionResult BundlesDisabled()
    {
        var response = new ApiResponse<object>
        {
            StatusCode = Common.StatusCode.UnauthorizedData,
            StatusMessage = Common.StatusCode.UnauthorizedData.ToString(),
            Description = BundleService.ErrBundlesDisabled,
        };
        return StatusCode((int)HttpStatusCode.Forbidden, response);
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public void SetAuthUser()
    {
        SetAuthUser(_bundleService);
    }
}
