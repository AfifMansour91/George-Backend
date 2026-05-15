using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "QuantityConcentrationReport")]
    [ApiController]
    public class QuantityConcentrationReportController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly QuantityConcentrationReportService _service;

        public QuantityConcentrationReportController(
            QuantityConcentrationReportService service,
            ILogger<QuantityConcentrationReportController> logger)
            : base(logger)
        {
            _service = service;
        }

        /// <summary>דוח ריכוז כמויות — הזמנות שאינן Delivered/Cancelled, לפי תאריך אספקה/איסוף בטווח.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.QuantityConcentrationReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            [FromQuery] DateTime from,
            [FromQuery] DateTime to,
            [FromQuery] int? categoryId = null,
            [FromQuery] bool includePicked = false,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _service.GetReportAsync(siteId, from, to, categoryId, includePicked, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_service);
        }
    }
}
