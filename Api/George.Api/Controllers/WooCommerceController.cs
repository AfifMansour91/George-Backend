using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "WooCommerce")]
    [ApiController]
    public class WooCommerceController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly WooCommerceService _wooCommerceService;

        public WooCommerceController(
            WooCommerceService wooCommerceService,
            ILogger<WooCommerceController> logger) : base(logger)
        {
            _wooCommerceService = wooCommerceService;
        }

        [HttpPost("Sync")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceSyncRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncToWooCommerceAsync(
            [FromBody] WooCommerceSyncReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _wooCommerceService.SyncToWooCommerceAsync(request, cancelToken));
        }

        [HttpPost("SyncCategory")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceCategorySyncRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncCategoryToWooCommerceAsync(
            [FromBody] WooCommerceSyncCategoryReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _wooCommerceService.SyncCategoryToWooCommerceAsync(
                    request.CategoryId, 
                    request.SiteId, 
                    cancelToken));
        }

        [HttpPost("SyncAttribute")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceAttributeSyncRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncAttributeToWooCommerceAsync(
            [FromBody] WooCommerceSyncAttributeReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _wooCommerceService.SyncAttributeToWooCommerceAsync(
                    request.AttributeId,
                    request.SiteId,
                    cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_wooCommerceService);
        }
    }
}

