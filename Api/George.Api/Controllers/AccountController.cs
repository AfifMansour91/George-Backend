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
        private readonly AccountSmsService _accountSmsSvc;

        public AccountController(AccountService accountSvc, AccountSmsService accountSmsSvc, ILogger<AccountController> logger) : base(logger)
        {
            _accountSvc = accountSvc;
            _accountSmsSvc = accountSmsSvc;
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

        /// <summary>Effective notification settings: with siteId returns the site's override if it exists (IsSiteOverride=true) or the account default; without siteId returns the account default.</summary>
        [HttpGet("{accountId:int}/notification-settings")]
        [ProducesResponseType(typeof(IApiResponse<SiteNotificationSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetNotificationSettingsAsync([FromRoute] int accountId, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.GetNotificationSettingsAsync(accountId, siteId, cancelToken));
        }

        /// <summary>Save notification settings. Without siteId updates the account default; with siteId creates/updates a FULL per-site override row.</summary>
        [HttpPut("{accountId:int}/notification-settings")]
        [ProducesResponseType(typeof(IApiResponse<SiteNotificationSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpsertNotificationSettingsAsync([FromRoute] int accountId, [FromQuery] int? siteId, [FromBody] NotificationSettingsReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.UpsertNotificationSettingsAsync(accountId, siteId, req, cancelToken));
        }

        /// <summary>Remove a site's notification-settings override; the site goes back to inheriting the account default.</summary>
        [HttpDelete("{accountId:int}/notification-settings")]
        [ProducesResponseType(typeof(IApiResponse<SiteNotificationSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteNotificationSettingsOverrideAsync([FromRoute] int accountId, [FromQuery] int siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSvc.DeleteNotificationSettingsOverrideAsync(accountId, siteId, cancelToken));
        }

        /// <summary>Per-account SMS account settings (token returned masked). No row / disabled = the account uses the system-wide SMS account.</summary>
        [HttpGet("{accountId:int}/sms-settings")]
        [ProducesResponseType(typeof(IApiResponse<AccountSmsSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSmsSettingsAsync([FromRoute] int accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSmsSvc.GetSettingsAsync(accountId, cancelToken));
        }

        /// <summary>Save per-account SMS credentials. An empty ApiToken keeps the stored token.</summary>
        [HttpPut("{accountId:int}/sms-settings")]
        [ProducesResponseType(typeof(IApiResponse<AccountSmsSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpsertSmsSettingsAsync([FromRoute] int accountId, [FromBody] AccountSmsSettingsReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSmsSvc.UpsertSettingsAsync(accountId, req, cancelToken));
        }

        /// <summary>Remove the account's SMS credentials; it goes back to the system-wide SMS account.</summary>
        [HttpDelete("{accountId:int}/sms-settings")]
        [ProducesResponseType(typeof(IApiResponse<AccountSmsSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteSmsSettingsAsync([FromRoute] int accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSmsSvc.DeleteSettingsAsync(accountId, cancelToken));
        }

        /// <summary>Send a test SMS using the account's saved SMS settings (save first, then test).</summary>
        [HttpPost("{accountId:int}/sms-settings/test")]
        [ProducesResponseType(typeof(IApiResponse<AccountSmsTestRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SendSmsTestAsync([FromRoute] int accountId, [FromBody] AccountSmsTestReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _accountSmsSvc.SendTestAsync(accountId, req, cancelToken));
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
