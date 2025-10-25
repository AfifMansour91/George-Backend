using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Services;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("Account/{accountId:long}/Products", Name = "AccountProduct")]
    [ApiController]
    public class AccountProductController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly AccountProductService _svc;

        public AccountProductController(AccountProductService svc, ILogger<AccountProductController> logger)
            : base(logger)
        {
            _svc = svc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<AccountProductListRow>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductsAsync(
            [FromRoute] long accountId,
            [FromQuery] ApiListReq<AccountProductListFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetAccountProductsAsync(accountId, request, cancelToken));
        }

        [HttpPut("{accountProductId:long}/Enabled")]
        [ProducesResponseType(typeof(IApiResponse<AccountProductToggleRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ToggleEnabledAsync(
            [FromRoute] long accountId,
            [FromRoute] long accountProductId,
            [FromBody] BoolReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.ToggleProductEnabledAsync(accountId, accountProductId, request, cancelToken));
        }

        [HttpGet("{accountProductId:long}")]
        [ProducesResponseType(typeof(IApiResponse<AccountProductDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductDetailAsync(
            [FromRoute] long accountId,
            [FromRoute] long accountProductId,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetAccountProductDetailAsync(accountId, accountProductId, cancelToken));
        }

        [HttpPut("{accountProductId:long}")]
        [ProducesResponseType(typeof(IApiResponse<AccountProductDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateProductAsync(
            [FromRoute] long accountId,
            [FromRoute] long accountProductId,
            [FromBody] AccountProductUpdateReq req,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.UpdateAccountProductAsync(accountId, accountProductId, req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_svc);
        }
    }
}
