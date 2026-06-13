using System.Net;
using System.Security.Claims;
using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace George.Api.Controllers;

/// <summary>
/// Inbound redemption reports from WordPress Promeng — GIORGIO_API §2.1 <c>POST /redemptions</c>.
/// Auth: site <c>InternalApiKey</c> via <c>X-Api-Key</c> or <c>Authorization: Bearer</c>.
/// </summary>
[Route("redemptions")]
[ApiController]
public class RedemptionController : GeorgeControllerBase
{
    private readonly PromotionService _promotionService;

    public RedemptionController(PromotionService promotionService, ILogger<RedemptionController> logger)
        : base(logger)
    {
        _promotionService = promotionService;
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = WooCommerceApiKeyAuthenticationHandler.SchemeName)]
    [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
    public async Task<IActionResult> RecordRedemptionsAsync(
        [FromBody] RecordPromotionRedemptionsReq req,
        CancellationToken cancelToken = default)
    {
        var siteIdClaim = User.FindFirstValue(WooCommerceApiKeyAuthenticationHandler.ClaimSiteId);
        if (!int.TryParse(siteIdClaim, out var siteId) || siteId <= 0)
            return Unauthorized();

        return await SafeCallWithErrorCatchingAsync(() =>
            _promotionService.RecordExternalRedemptionsAsync(siteId, req, cancelToken));
    }
}
