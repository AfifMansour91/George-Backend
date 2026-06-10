using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "RevenueReport")]
    [ApiController]
    public class RevenueReportController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly RevenueReportService _revenueReportService;

        public RevenueReportController(
            RevenueReportService revenueReportService,
            ILogger<RevenueReportController> logger)
            : base(logger)
        {
            _revenueReportService = revenueReportService;
        }

        /// <summary>דוח הכנסות — KPIs פיננסיים, מגמה, פילוחים (לפי חיוב / לפי הזמנה).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.RevenueReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            [FromQuery] string period = "month",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string dateBasis = "charge",
            [FromQuery] string? compare = "prev_month",
            [FromQuery] string? search = null,
            [FromQuery] string? channels = null,
            [FromQuery] string? paymentMethods = null,
            [FromQuery] string? statuses = null,
            [FromQuery] string? cities = null,
            [FromQuery] string? categoryIds = null,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _revenueReportService.GetReportAsync(
                    siteId, period, from, to, dateBasis, compare, search,
                    channels, paymentMethods, statuses, cities, categoryIds, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_revenueReportService);
        }
    }
}
