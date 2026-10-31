using George.Api.Core;
using George.Common;
using George.Services.AiStudio;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

/// <summary>
/// AI Product Studio (AI_PRODUCT_STUDIO_SPEC.md): turn a shop's phone photo into a studio-grade product image. Requires
/// <c>Account.AiStudioEnabled</c>; generation runs in the background and progress arrives on the <c>/hubs/aistudio</c> SignalR hub
/// (<c>AiJobUpdated</c>) or by polling <c>GET /AiStudio/Jobs/{id}</c>.
/// </summary>
[Route("[controller]", Name = "AiStudio")]
[ApiController]
public class AiStudioController : GeorgeControllerBase, IAuthUserProvider
{
    private readonly AiStudioService _service;
    private readonly AiCreditService _credits;

    public AiStudioController(AiStudioService service, AiCreditService credits, ILogger<AiStudioController> logger) : base(logger)
    {
        _service = service;
        _credits = credits;
    }

    /// <summary>GET /AiStudio/Status - enabled?, mode, looks + credits, daily usage.</summary>
    [HttpGet("Status")]
    [ProducesResponseType(typeof(IApiResponse<AiStudioStatusRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetStatusAsync([FromQuery] int? accountId, [FromQuery] int? siteId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.GetStatusAsync(new AiStudioScopeReq { AccountId = accountId, SiteId = siteId }, cancelToken));
    }

    /// <summary>POST /AiStudio/Jobs - queue one look on one photo. Returns immediately with status <c>queued</c>.</summary>
    [HttpPost("Jobs")]
    [ProducesResponseType(typeof(IApiResponse<AiJobRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreateJobAsync([FromBody] AiJobCreateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.CreateJobAsync(req, cancelToken));
    }

    /// <summary>POST /AiStudio/Jobs/List - recent jobs of the account (optionally one product).</summary>
    /// <summary>POST /AiStudio/Jobs/ForProducts - "צור תמונות למוצרים ללא תמונה": one generate job per product (up to 50), attached
    /// to the product when done. Products that already have an image are skipped.</summary>
    [HttpPost("Jobs/ForProducts")]
    [ProducesResponseType(typeof(IApiResponse<AiJobsForProductsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreateJobsForProductsAsync([FromBody] AiJobsForProductsReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.CreateJobsForProductsAsync(req, cancelToken));
    }

    /// <summary>GET /AiStudio/Presets - the shop's own settings per look (active, style notes, reference images).</summary>
    [HttpGet("Presets")]
    [ProducesResponseType(typeof(IApiResponse<List<AiAccountLookRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPresetsAsync([FromQuery] int? accountId, [FromQuery] int? siteId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.GetAccountLooksAsync(new AiStudioScopeReq { AccountId = accountId, SiteId = siteId }, cancelToken));
    }

    /// <summary>PUT /AiStudio/Presets - save the shop's settings of one look. Defaults (active, no notes, no references) delete the row.</summary>
    [HttpPut("Presets")]
    [ProducesResponseType(typeof(IApiResponse<List<AiAccountLookRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SavePresetAsync([FromBody] AiAccountLookSaveReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.SaveAccountLookAsync(req, cancelToken));
    }

    [HttpPost("Jobs/List")]
    [ProducesResponseType(typeof(IApiResponse<List<AiJobRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ListJobsAsync([FromBody] AiJobListReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.ListJobsAsync(req, cancelToken));
    }

    [HttpGet("Jobs/{id:guid}")]
    [ProducesResponseType(typeof(IApiResponse<AiJobRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetJobAsync([FromRoute] Guid id, [FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.GetJobAsync(id, accountId, cancelToken));
    }

    /// <summary>POST /AiStudio/Jobs/{id}/Retry - same photo and look again; the first retry is free.</summary>
    [HttpPost("Jobs/{id:guid}/Retry")]
    [ProducesResponseType(typeof(IApiResponse<AiJobRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> RetryJobAsync([FromRoute] Guid id, [FromBody] AiStudioScopeReq? req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.RetryJobAsync(id, req?.AccountId, cancelToken));
    }

    [HttpPost("Jobs/{id:guid}/Cancel")]
    [ProducesResponseType(typeof(IApiResponse<AiJobRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CancelJobAsync([FromRoute] Guid id, [FromBody] AiStudioScopeReq? req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.CancelJobAsync(id, req?.AccountId, cancelToken));
    }

    /// <summary>POST /AiStudio/Jobs/{id}/Rate - 👍 / 👎 on the result (feeds prompt tuning).</summary>
    [HttpPost("Jobs/{id:guid}/Rate")]
    [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> RateJobAsync([FromRoute] Guid id, [FromBody] AiJobRateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.RateJobAsync(id, req, cancelToken));
    }

    /// <summary>POST /AiStudio/Suggest - name / descriptions / category / brand / weight suggestion from a photo. Synchronous (5-8 s).</summary>
    [HttpPost("Suggest")]
    [ProducesResponseType(typeof(IApiResponse<AiSuggestRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SuggestAsync([FromBody] AiSuggestReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.SuggestAsync(req, cancelToken));
    }

    /// <summary>POST /AiStudio/PoolConsent - the shop accepted the community-pool terms. Returns the refreshed status.</summary>
    [HttpPost("PoolConsent")]
    [ProducesResponseType(typeof(IApiResponse<AiStudioStatusRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SetPoolConsentAsync([FromBody] AiStudioScopeReq? req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _service.SetPoolConsentAsync(req ?? new AiStudioScopeReq(), cancelToken));
    }

    //*************************    Credits (phase 3)    *************************//

    /// <summary>GET /AiStudio/Credits - balance, ledger and purchases of the account.</summary>
    [HttpGet("Credits")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetCreditsAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.GetCreditsAsync(accountId, cancelToken));
    }

    /// <summary>GET /AiStudio/Packages - the active credit packages (prices before VAT).</summary>
    [HttpGet("Packages")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditPackagesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPackagesAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.GetPackagesAsync(accountId, cancelToken));
    }

    /// <summary>GET /AiStudio/Packages/Admin - super-admin: the whole catalog, inactive rows included.</summary>
    [HttpGet("Packages/Admin")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditPackagesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPackagesForAdminAsync(CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.GetPackagesForAdminAsync(cancelToken));
    }

    /// <summary>PUT /AiStudio/Packages/Admin - super-admin: replace the catalog.</summary>
    [HttpPut("Packages/Admin")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditPackagesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SavePackagesAsync([FromBody] List<AiCreditPackageSaveReq> req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.SavePackagesAsync(req, cancelToken));
    }

    /// <summary>POST /AiStudio/Purchases - open Cardcom's hosted page for one package (the platform's terminal).</summary>
    [HttpPost("Purchases")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditPurchaseStartRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> StartPurchaseAsync([FromBody] AiCreditPurchaseStartReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.StartPurchaseAsync(req, cancelToken));
    }

    /// <summary>POST /AiStudio/Purchases/{purchaseId}/Confirm - the return page settles the payment (the webhook may already have).</summary>
    [HttpPost("Purchases/{purchaseId:int}/Confirm")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditPurchaseRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ConfirmPurchaseAsync([FromRoute] int purchaseId, [FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.ConfirmPurchaseAsync(purchaseId, accountId, cancelToken));
    }

    [HttpGet("Purchases")]
    [ProducesResponseType(typeof(IApiResponse<List<AiCreditPurchaseRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ListPurchasesAsync([FromQuery] int? accountId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.ListPurchasesAsync(accountId, cancelToken));
    }

    /// <summary>POST /AiStudio/Accounts/{accountId}/Credits/Allocate - super-admin: load or correct a shop's credits.</summary>
    [HttpPost("Accounts/{accountId:int}/Credits/Allocate")]
    [ProducesResponseType(typeof(IApiResponse<AiCreditsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> AllocateCreditsAsync([FromRoute] int accountId, [FromBody] AiCreditAllocateReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.AllocateAsync(accountId, req, cancelToken));
    }

    /// <summary>GET /AiStudio/Admin/Overview - super-admin: jobs, cost, credits and revenue per account for a period.</summary>
    [HttpGet("Admin/Overview")]
    [ProducesResponseType(typeof(IApiResponse<AiStudioAdminOverviewRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAdminOverviewAsync([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.GetAdminOverviewAsync(fromUtc, toUtc, cancelToken));
    }

    /// <summary>GET /AiStudio/Admin/Jobs - super-admin: the job log, newest first, filtered by account / status / period, paged.
    /// status = queued | running | preview_ready | done | failed | canceled | active (anything not finished).</summary>
    [HttpGet("Admin/Jobs")]
    [ProducesResponseType(typeof(IApiResponse<AiStudioAdminJobsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAdminJobsAsync([FromQuery] int? accountId, [FromQuery] string? status, [FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, [FromQuery] int skip = 0, [FromQuery] int take = 20, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.GetAdminJobsAsync(accountId, status, fromUtc, toUtc, skip, take, cancelToken));
    }

    /// <summary>POST /AiStudio/Admin/Providers/TopUp - super-admin notes the balance shown in a provider console; the wallet
    /// estimate on the admin page re-anchors on it (no provider exposes a balance API).</summary>
    [HttpPost("Admin/Providers/TopUp")]
    [ProducesResponseType(typeof(IApiResponse<AiProviderWalletRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> TopUpProviderAsync([FromBody] AiProviderTopUpReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.TopUpAsync(req, cancelToken));
    }

    /// <summary>GET /AiStudio/Admin/Looks - super-admin: every look with its current prompt / provider / price, the code default
    /// behind it and the period's quality numbers (judge scores, thumbs, recurring issues) for prompt tuning.</summary>
    [HttpGet("Admin/Looks")]
    [ProducesResponseType(typeof(IApiResponse<AiLooksAdminRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetAdminLooksAsync([FromQuery] DateTime? fromUtc, [FromQuery] DateTime? toUtc, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.GetAdminLooksAsync(fromUtc, toUtc, cancelToken));
    }

    /// <summary>PUT /AiStudio/Admin/Looks - super-admin: save one look's global preset (empty fields = code default). Applies at once.</summary>
    [HttpPut("Admin/Looks")]
    [ProducesResponseType(typeof(IApiResponse<AiLooksAdminRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SaveAdminLookAsync([FromBody] AiLookPresetSaveReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _credits.SaveAdminLookAsync(req, cancelToken));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public void SetAuthUser()
    {
        SetAuthUser(_service);
        SetAuthUser(_credits);
    }
}
