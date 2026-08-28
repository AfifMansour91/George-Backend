using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "OrdersReport")]
    [ApiController]
    public class OrdersReportController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly OrdersReportService _service;

        public OrdersReportController(
            OrdersReportService service,
            ILogger<OrdersReportController> logger)
            : base(logger)
        {
            _service = service;
        }

        /// <summary>
        /// דוח הזמנות - הזמנות שאינן Cancelled בטווח תאריכים (ברירת מחדל: היום), לפי תאריך אספקה או תאריך הזמנה.
        /// </summary>
        /// <param name="dateBasis"><c>supply</c> (default) - effective delivery/pickup date; <c>order</c> - creation date.</param>
        /// <param name="fulfillment"><c>all</c> (default), <c>supplied</c>, or <c>notSupplied</c>.</param>
        /// <param name="deliveryType"><c>all</c> (default), <c>shipping</c>, or <c>pickup</c>.</param>
        /// <param name="paymentKind"><c>all</c> (default), <c>cash</c>, or <c>credit</c>.</param>
        /// <param name="cities">CSV of city names; <c>__none__</c> = orders without a delivery city.</param>
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<George.Services.Response.OrdersReportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAsync(
            [FromQuery] int siteId,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] string? dateBasis = null,
            [FromQuery] string? fulfillment = null,
            [FromQuery] string? deliveryType = null,
            [FromQuery] string? paymentKind = null,
            [FromQuery] string? cities = null,
            [FromQuery] string? search = null,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _service.GetReportAsync(
                    siteId, from, to, dateBasis, fulfillment, deliveryType, paymentKind, cities, search, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_service);
        }
    }
}
