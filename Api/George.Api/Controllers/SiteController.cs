using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Site")]
    [ApiController]
    public class SiteController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly SiteService _siteSvc;

        public SiteController(SiteService siteSvc, ILogger<SiteController> logger) : base(logger)
        {
            _siteSvc = siteSvc;
        }

        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<SiteFilter>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSitesAsync(
            [FromQuery] ApiListReq<SiteFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _siteSvc.GetSitesAsync(request, cancelToken));
        }

        [HttpGet("{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<SiteRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSiteAsync([FromRoute] int siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _siteSvc.GetSiteAsync(siteId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<SiteRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateSiteAsync([FromBody] CreateSiteReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _siteSvc.CreateSiteAsync(req, cancelToken));
        }

        [HttpPut("{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<SiteRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateSiteAsync([FromRoute] int siteId, [FromBody] UpdateSiteReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _siteSvc.UpdateSiteAsync(siteId, req, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<List<SiteRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSitesByAccountAsync([FromRoute] int accountId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _siteSvc.GetSitesByAccountAsync(accountId, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_siteSvc);
        }
    }
}
