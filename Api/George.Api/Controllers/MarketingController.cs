using George.Api.Core;
using George.Common;
using George.Services.Marketing;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

/// <summary>
/// Marketing module (שיווק) - segments, the send wizard, results, quota and settings. Every action resolves
/// the caller's account + branch scope in <see cref="MarketingService"/> and requires
/// <c>Account.MarketingEnabled</c>; bodies carry the scope (<c>accountId</c> is honoured for unrestricted callers only).
/// </summary>
[Route("[controller]", Name = "Marketing")]
[ApiController]
public class MarketingController : GeorgeControllerBase, IAuthUserProvider
{
    private readonly MarketingService _marketingService;

    public MarketingController(MarketingService marketingService, ILogger<MarketingController> logger)
        : base(logger)
    {
        _marketingService = marketingService;
    }

    //*************************    Segments    *************************//

    /// <summary>POST /Marketing/Segments/List - ready-made + saved segments with their two counts, inside the branch scope.</summary>
    [HttpPost("Segments/List")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSegmentsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSegmentsAsync([FromBody] MarketingScopeReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetSegmentsAsync(req, cancelToken));
    }

    /// <summary>POST /Marketing/Segments/Preview - counts + a page of customers for any audience (also the live filter of "create segment").</summary>
    [HttpPost("Segments/Preview")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSegmentPreviewRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> PreviewSegmentAsync([FromBody] MarketingSegmentPreviewReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.PreviewSegmentAsync(req, cancelToken));
    }

    [HttpPost("Segments")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSegmentRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreateSegmentAsync([FromBody] MarketingSegmentSaveReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.SaveSegmentAsync(null, req, cancelToken));
    }

    [HttpPut("Segments/{segmentId:int}")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSegmentRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdateSegmentAsync([FromRoute] int segmentId, [FromBody] MarketingSegmentSaveReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.SaveSegmentAsync(segmentId, req, cancelToken));
    }

    [HttpDelete("Segments/{segmentId:int}")]
    [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> DeleteSegmentAsync([FromRoute] int segmentId, [FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.DeleteSegmentAsync(segmentId, accountId, cancelToken));
    }

    /// <summary>POST /Marketing/Cities - customer cities for the geo axis picker.</summary>
    [HttpPost("Cities")]
    [ProducesResponseType(typeof(IApiResponse<List<string>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetCitiesAsync([FromBody] MarketingScopeReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetCitiesAsync(req, cancelToken));
    }

    //*************************    Send wizard    *************************//

    /// <summary>POST /Marketing/Sends/Estimate - the wizard's side panel: audience vs. will-send, units, quota, send window, rendered preview.</summary>
    [HttpPost("Sends/Estimate")]
    [ProducesResponseType(typeof(IApiResponse<MarketingEstimateRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> EstimateAsync([FromBody] MarketingEstimateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.EstimateAsync(req, cancelToken));
    }

    /// <summary>POST /Marketing/Sends/Test - "שלח לעצמי לבדיקה".</summary>
    [HttpPost("Sends/Test")]
    [ProducesResponseType(typeof(IApiResponse<MarketingTestSendRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendTestAsync([FromBody] MarketingTestSendReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.SendTestAsync(req, cancelToken));
    }

    /// <summary>POST /Marketing/Sends - create a send (now, or at <c>scheduledAt</c>). The dispatcher picks it up.</summary>
    [HttpPost("Sends")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSendRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreateSendAsync([FromBody] MarketingSendCreateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.CreateSendAsync(req, cancelToken));
    }

    //*************************    Results    *************************//

    /// <summary>Edit a scheduled send (allowed until 10 minutes before it goes out).</summary>
    [HttpPut("Sends/{sendId:int}")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSendRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdateSendAsync([FromRoute] int sendId, [FromBody] MarketingSendCreateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.UpdateSendAsync(sendId, req ?? new MarketingSendCreateReq(), cancelToken));
    }

    /// <summary>Attributed revenue per week (results home chart) + previous-period totals for the trend.</summary>
    [HttpPost("Sends/Weekly")]
    [ProducesResponseType(typeof(IApiResponse<MarketingWeeklyRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetWeeklyRevenueAsync([FromBody] MarketingSendListReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetWeeklyRevenueAsync(req ?? new MarketingSendListReq(), cancelToken));
    }

    [HttpPost("Sends/List")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSendListRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ListSendsAsync([FromBody] MarketingSendListReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.ListSendsAsync(req, cancelToken));
    }

    [HttpGet("Sends/{sendId:int}")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSendRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSendAsync([FromRoute] int sendId, [FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetSendAsync(sendId, accountId, cancelToken));
    }

    /// <summary>GET /Marketing/Sends/{id}/Deliveries?tab=ordered|clicked|failed|unsubscribed|skipped|all</summary>
    [HttpGet("Sends/{sendId:int}/Deliveries")]
    [ProducesResponseType(typeof(IApiResponse<MarketingDeliveryListRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetDeliveriesAsync([FromRoute] int sendId, [FromQuery] int? accountId, [FromQuery] string? tab,
        [FromQuery] int skip = 0, [FromQuery] int take = 50, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetDeliveriesAsync(sendId, accountId, tab, skip, take, cancelToken));
    }

    [HttpPost("Sends/{sendId:int}/Cancel")]
    [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CancelSendAsync([FromRoute] int sendId, [FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.CancelSendAsync(sendId, accountId, cancelToken));
    }

    //*************************    Quota / settings / usage    *************************//

    [HttpGet("Quota")]
    [ProducesResponseType(typeof(IApiResponse<MarketingQuotaRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetQuotaAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetQuotaAsync(accountId, cancelToken));
    }

    /// <summary>POST /Marketing/Quota/{accountId}/Allocate - super-admin: load the account's marketing bank.</summary>
    [HttpPost("Quota/{accountId:int}/Allocate")]
    [ProducesResponseType(typeof(IApiResponse<MarketingQuotaRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> AllocateQuotaAsync([FromRoute] int accountId, [FromBody] MarketingQuotaAllocateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.AllocateQuotaAsync(accountId, req, cancelToken));
    }

    [HttpGet("Settings")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSettingsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSettingsAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetSettingsAsync(accountId, cancelToken));
    }

    [HttpPut("Settings")]
    [ProducesResponseType(typeof(IApiResponse<MarketingSettingsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdateSettingsAsync([FromQuery] int? accountId, [FromBody] MarketingSettingsReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.UpdateSettingsAsync(accountId, req, cancelToken));
    }

    /// <summary>GET /Marketing/Usage - what actually went out in the last six months, operational vs. marketing.</summary>
    [HttpGet("Usage")]
    [ProducesResponseType(typeof(IApiResponse<MarketingUsageRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetUsageAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetUsageAsync(accountId, cancelToken));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public void SetAuthUser()
    {
        SetAuthUser(_marketingService);
    }
}
