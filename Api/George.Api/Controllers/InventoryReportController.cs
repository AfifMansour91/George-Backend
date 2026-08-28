using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "InventoryReport")]
    [ApiController]
    public class InventoryReportController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly InventoryReportService _service;

        public InventoryReportController(
            InventoryReportService service,
            ILogger<InventoryReportController> logger)
            : base(logger)
        {
            _service = service;
        }

        /// <summary>דוח מלאי - מוצרי קטלוג לאתר, מלאי ווריאציות, ספקים ומותגים לסינון.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.InventoryReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _service.GetReportAsync(siteId, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_service);
        }
    }
}
