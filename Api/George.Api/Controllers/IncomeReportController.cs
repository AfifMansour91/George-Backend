using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "IncomeReport")]
    [ApiController]
    public class IncomeReportController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly IncomeReportService _incomeReportService;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public IncomeReportController(
            IncomeReportService incomeReportService,
            IWebHostEnvironment environment,
            IConfiguration configuration,
            ILogger<IncomeReportController> logger)
            : base(logger)
        {
            _incomeReportService = incomeReportService;
            _environment = environment;
            _configuration = configuration;
        }

        /// <summary>דוח הכנסות — נתונים מחושבים בשרת לפי פילטרים (תקופה, קטגוריה, קופון, השוואת KPI).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.IncomeReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            [FromQuery] string period = "month",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] string? city = null,
            [FromQuery] string? coupon = null,
            [FromQuery] string? kpiCompare = null,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _incomeReportService.GetReportAsync(siteId, period, from, to, categoryId, city, coupon, kpiCompare, cancelToken));
        }

        /// <summary>
        /// Public coupon income view (no JWT). Token must be <c>demo</c> in Development only, or match configuration
        /// <c>IncomeReport:PublicCouponToken</c> in any environment.
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.IncomeReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetPublicAsync(
            [FromQuery] int siteId,
            [FromQuery] string coupon,
            [FromQuery] string token,
            [FromQuery] string period = "month",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            CancellationToken cancelToken = default)
        {
            if (!IsPublicCouponShareTokenValid(token))
                return CreateHttpResponse(George.Common.StatusCode.UnauthorizedData, "Invalid or expired share link.");
            if (siteId <= 0 || string.IsNullOrWhiteSpace(coupon))
                return CreateHttpResponse(George.Common.StatusCode.InvalidRequest, "siteId and coupon are required.");

            var couponTrim = coupon.Trim();
            return await SafeCallWithErrorCatchingAsync(() =>
                _incomeReportService.GetReportAsync(siteId, period, from, to, null, null, couponTrim, null, cancelToken));
        }

        private bool IsPublicCouponShareTokenValid(string? token)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var t = token.Trim();
            if (string.Equals(t, "demo", StringComparison.OrdinalIgnoreCase))
                return _environment.IsDevelopment();
            var configured = _configuration["IncomeReport:PublicCouponToken"];
            return !string.IsNullOrWhiteSpace(configured) && string.Equals(t, configured, StringComparison.Ordinal);
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_incomeReportService);
        }
    }
}
