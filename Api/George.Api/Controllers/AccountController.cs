using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Services;
using George.Services.Response;
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

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<CreateAccountRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateAccountAsync([FromBody] CreateAccountReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.CreateAccountAsync(req, cancelToken));
        }

        [HttpGet("{accountId:long}")]
        [ProducesResponseType(typeof(IApiResponse<AccountRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAccountAsync([FromRoute] long accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.GetAccountAsync(accountId, cancelToken));
        }

        [HttpPut("{accountId:long}")]
        [ProducesResponseType(typeof(IApiResponse<AccountRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateAccountAsync([FromRoute] long accountId, [FromBody] UpdateAccountReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.UpdateAccountAsync(accountId, req, cancelToken));
        }

        [HttpGet("{accountId:long}/WizardSession")]
        [ProducesResponseType(typeof(IApiResponse<WizardSessionRes>), 200)]
        public async Task<IActionResult> GetWizardSessionAsync([FromRoute] long accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.GetWizardSessionAsync(accountId, cancelToken));
        }

        [HttpPut("{accountId:long}/WizardSession")]
        [ProducesResponseType(typeof(IApiResponse<WizardSessionRes>), 200)]
        public async Task<IActionResult> UpdateWizardSessionAsync([FromRoute] long accountId, [FromBody] UpdateWizardSessionReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.UpdateWizardSessionAsync(accountId, req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_accountSvc);
        }
    }
}
