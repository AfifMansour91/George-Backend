using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

[Route("[controller]", Name = "Promotion")]
[ApiController]
public class PromotionController : GeorgeControllerBase, IAuthUserProvider
{
    private readonly PromotionService _promotionService;

    public PromotionController(PromotionService promotionService, ILogger<PromotionController> logger)
        : base(logger)
    {
        _promotionService = promotionService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IApiResponse<ApiListResponse<PromotionRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPromotionsAsync(
        [FromQuery] ApiListReq<PromotionFilter> request,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.GetPromotionsAsync(request, cancelToken));
    }

    [HttpGet("stats")]
    [ProducesResponseType(typeof(IApiResponse<PromotionStatsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPromotionStatsAsync(
        [FromQuery] PromotionStatsFilter filter,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.GetPromotionStatsAsync(filter, cancelToken));
    }

    [HttpGet("{promotionId:int}")]
    [ProducesResponseType(typeof(IApiResponse<PromotionRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetPromotionAsync([FromRoute] int promotionId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.GetPromotionAsync(promotionId, cancelToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(IApiResponse<PromotionRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreatePromotionAsync([FromBody] CreatePromotionReq req, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.CreatePromotionAsync(req, cancelToken));
    }

    [HttpPut("{promotionId:int}")]
    [ProducesResponseType(typeof(IApiResponse<PromotionRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdatePromotionAsync(
        [FromRoute] int promotionId,
        [FromBody] UpdatePromotionReq req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.UpdatePromotionAsync(promotionId, req, cancelToken));
    }

    [HttpPatch("{promotionId:int}/status")]
    [ProducesResponseType(typeof(IApiResponse<PromotionRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdatePromotionStatusAsync(
        [FromRoute] int promotionId,
        [FromBody] UpdatePromotionStatusReq req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.UpdatePromotionStatusAsync(promotionId, req, cancelToken));
    }

    [HttpDelete("{promotionId:int}")]
    [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> DeletePromotionAsync([FromRoute] int promotionId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.DeletePromotionAsync(promotionId, cancelToken));
    }

    /// <summary>
    /// Evaluate which promotions apply to a given cart (storefront/kiosk integration).
    /// Spec: <c>Sprint4/מבצעים.md</c>.
    /// </summary>
    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(IApiResponse<EvaluatePromotionsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> EvaluatePromotionsAsync(
        [FromBody] EvaluatePromotionsReq req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.EvaluatePromotionsAsync(req, cancelToken));
    }

    /// <summary>Products/categories that should show a catalog "במבצע" badge (ShowBadge promotions).</summary>
    [HttpGet("catalog-badges")]
    [ProducesResponseType(typeof(IApiResponse<PromotionCatalogBadgesRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetCatalogBadgesAsync(
        [FromQuery] int siteId,
        [FromQuery] string? channel,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _promotionService.GetCatalogBadgesAsync(siteId, channel, cancelToken));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public void SetAuthUser()
    {
        SetAuthUser(_promotionService);
    }
}
