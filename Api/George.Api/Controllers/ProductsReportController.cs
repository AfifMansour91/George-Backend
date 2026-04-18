using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "ProductsReport")]
    [ApiController]
    public class ProductsReportController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly ProductsReportService _productsReportService;

        public ProductsReportController(
            ProductsReportService productsReportService,
            ILogger<ProductsReportController> logger)
            : base(logger)
        {
            _productsReportService = productsReportService;
        }

        /// <summary>דוח מוצרים וביצועים — KPIs, טבלה, פילוח, אפסלים (לפי הזמנות בתקופה).</summary>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.ProductsReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            [FromQuery] string period = "month",
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int? categoryId = null,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _productsReportService.GetReportAsync(siteId, period, from, to, categoryId, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_productsReportService);
        }
    }
}
