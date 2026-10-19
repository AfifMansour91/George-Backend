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
    /// <summary>Super-admin: open an Inforu sub-account for the shop and save it (one step).</summary>
    [HttpPost("Quota/{accountId:int}/Inforu/Provision")]
    [ProducesResponseType(typeof(IApiResponse<AccountSmsSettingsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ProvisionInforuAsync([FromRoute] int accountId, [FromBody] MarketingInforuProvisionReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.ProvisionInforuSubAccountAsync(accountId, req ?? new MarketingInforuProvisionReq(), cancelToken));
    }

    /// <summary>Super-admin: set the sub-account's quota type (monthly renewal / packages) and amount in Inforu.</summary>
    [HttpPost("Quota/{accountId:int}/Inforu/SetQuota")]
    [ProducesResponseType(typeof(IApiResponse<MarketingQuotaRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SetInforuQuotaAsync([FromRoute] int accountId, [FromBody] MarketingInforuQuotaReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.SetInforuQuotaAsync(accountId, req ?? new MarketingInforuQuotaReq(), cancelToken));
    }

    [HttpPost("Quota/{accountId:int}/Allocate")]
    [ProducesResponseType(typeof(IApiResponse<MarketingQuotaRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> AllocateQuotaAsync([FromRoute] int accountId, [FromBody] MarketingQuotaAllocateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.AllocateQuotaAsync(accountId, req, cancelToken));
    }

    //*************************    Packages / self-service purchase    *************************//

    /// <summary>GET /Marketing/Packages - the packages a shop can buy (prices before VAT + the charged total).</summary>
    [HttpGet("Packages")]
    [ProducesResponseType(typeof(IApiResponse<MarketingPackagesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPackagesAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetPackagesAsync(accountId, includeInactive: false, cancelToken));
    }

    /// <summary>Super-admin: the whole catalog, inactive rows included.</summary>
    [HttpGet("Packages/Admin")]
    [ProducesResponseType(typeof(IApiResponse<MarketingPackagesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPackagesForAdminAsync(CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetPackagesForAdminAsync(cancelToken));
    }

    /// <summary>Super-admin: replace the catalog (rows left out are deactivated, never deleted).</summary>
    [HttpPut("Packages/Admin")]
    [ProducesResponseType(typeof(IApiResponse<MarketingPackagesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SavePackagesAsync([FromBody] List<MarketingPackageSaveReq> req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.SavePackagesAsync(req, cancelToken));
    }

    /// <summary>GET /Marketing/Admin/Overview - super-admin: SMS volumes, banks and package revenue per account for a period (default: this month).</summary>
    [HttpGet("Admin/Overview")]
    [ProducesResponseType(typeof(IApiResponse<MarketingAdminOverviewRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAdminOverviewAsync([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.GetAdminOverviewAsync(fromUtc, toUtc, cancelToken));
    }

    /// <summary>POST /Marketing/Purchases - account admin starts buying a package; answers with the Cardcom hosted page URL.</summary>
    [HttpPost("Purchases")]
    [ProducesResponseType(typeof(IApiResponse<MarketingPurchaseStartRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> StartPurchaseAsync([FromBody] MarketingPurchaseStartReq req, CancellationToken cancelToken = default)
    {
        var appOrigin = string.IsNullOrWhiteSpace(req?.AppOrigin) ? Request.Headers.Origin.FirstOrDefault() : req!.AppOrigin;
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.StartPurchaseAsync(new MarketingPurchaseStartReq
        {
            AccountId = req?.AccountId, PackageId = req?.PackageId ?? 0, AppOrigin = appOrigin,
        }, cancelToken));
    }

    /// <summary>POST /Marketing/Purchases/{id}/Confirm - the return page asks for the outcome (settles if the webhook has not).</summary>
    [HttpPost("Purchases/{purchaseId:int}/Confirm")]
    [ProducesResponseType(typeof(IApiResponse<MarketingPurchaseRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ConfirmPurchaseAsync([FromRoute] int purchaseId, [FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.ConfirmPurchaseAsync(purchaseId, accountId, cancelToken));
    }

    /// <summary>GET /Marketing/Purchases - the account's purchase history (newest first).</summary>
    [HttpGet("Purchases")]
    [ProducesResponseType(typeof(IApiResponse<List<MarketingPurchaseRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ListPurchasesAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _marketingService.ListPurchasesAsync(accountId, cancelToken));
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
