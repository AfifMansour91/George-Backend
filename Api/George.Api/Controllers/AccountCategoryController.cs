using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Services;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("Account/{accountId:long}/Categories", Name = "AccountCategory")]
    [ApiController]
    public class AccountCategoryController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly AccountCategoryService _svc;

        public AccountCategoryController(AccountCategoryService svc, ILogger<AccountCategoryController> logger)
            : base(logger)
        {
            _svc = svc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<List<AccountCategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCategoriesAsync([FromRoute] long accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetAccountCategoriesAsync(accountId, cancelToken));
        }

        [HttpPut]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateCategoriesAsync([FromRoute] long accountId, [FromBody] UpdateAccountCategoriesReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.UpdateAccountCategoriesAsync(accountId, req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_svc);
        }
    }
}
