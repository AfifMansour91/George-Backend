using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Services;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Account")]
    [ApiController]
    public class AccountController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly AccountService _accountSvc;

        public AccountController(AccountService accountSvc, ILogger<AccountController> logger) : base(logger)
        {
            _accountSvc = accountSvc;
        }

        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<AccountFilter>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAccountsAsync(
            [FromQuery] ApiListReq<AccountFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.GetAccountsAsync(request, cancelToken));
        }

        [HttpGet("{accountId:long}")]
        [ProducesResponseType(typeof(IApiResponse<AccountRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAccountAsync([FromRoute] long accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.GetAccountAsync(accountId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<CreateAccountRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateAccountAsync([FromBody] CreateAccountReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.CreateAccountAsync(req, cancelToken));
        }

        [HttpPut("{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<AccountRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateAccountAsync([FromRoute] int accountId, [FromBody] UpdateAccountReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.UpdateAccountAsync(accountId, req, cancelToken));
        }

        [HttpDelete("{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<AccountRes?>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteAccountAsync([FromRoute] int accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.DeleteAccountAsync(accountId, cancelToken));
        }

        [HttpGet("{accountId:int}/wizard-session")]
        [ProducesResponseType(typeof(IApiResponse<WizardSessionRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetWizardSessionAsync([FromRoute] int accountId, [FromQuery] string? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.GetWizardSessionAsync(accountId, siteId, cancelToken));
        }

        [HttpPut("{accountId:int}/wizard-session")]
        [ProducesResponseType(typeof(IApiResponse<WizardSessionRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateWizardSessionAsync([FromRoute] int accountId, [FromQuery] string? siteId, [FromBody] UpdateWizardSessionReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.UpdateWizardSessionAsync(accountId, siteId, req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_accountSvc);
        }
    }
}
