using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "IntegrationLog")]
    [ApiController]
    public class IntegrationLogController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly IntegrationLogService _integrationLogService;

        public IntegrationLogController(
            IntegrationLogService integrationLogService,
            ILogger<IntegrationLogController> logger)
            : base(logger)
        {
            _integrationLogService = integrationLogService;
        }

        /// <summary>
        /// Sync logs for a site (admin screen): what George sent to / received from the store plus in-app
        /// order events, newest first and paged. All filters optional; <c>search</c> matches external id /
        /// operation / url / error.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<IntegrationLogRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            [FromQuery] string? entityType = null,
            [FromQuery] string? direction = null,
            [FromQuery] string? level = null,
            [FromQuery] bool? success = null,
            [FromQuery] string? search = null,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _integrationLogService.GetLogsAsync(siteId, entityType, direction, level, success, search, skip, take, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_integrationLogService);
        }
    }
}
