using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Order")]
    [ApiController]
    public class OrderController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly OrderService _orderSvc;

        public OrderController(OrderService orderSvc, ILogger<OrderController> logger) : base(logger)
        {
            _orderSvc = orderSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<OrderRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetOrdersAsync(
            [FromQuery] ApiListReq<OrderFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.GetOrdersAsync(request, cancelToken));
        }

        /// <summary>Aggregated handling medians for a site and date range (dashboard). Literal route before {orderId}.</summary>
        [HttpGet("HandlingMetrics")]
        [ProducesResponseType(typeof(IApiResponse<OrderHandlingMetricsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetHandlingMetricsAsync(
            [FromQuery] int siteId,
            [FromQuery] string from,
            [FromQuery] string to,
            CancellationToken cancelToken = default)
        {
            if (!DateTime.TryParse(from, out var fromDate) || !DateTime.TryParse(to, out var toDate))
                return BadRequest("from and to must be valid dates (yyyy-MM-dd).");
            return await SafeCallWithErrorCatchingAsync(() =>
                _orderSvc.GetOrderHandlingMetricsAsync(siteId, fromDate, toDate, cancelToken));
        }

        [HttpGet("{orderId:int}")]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetOrderAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.GetOrderAsync(orderId, cancelToken));
        }

        /// <summary>Status timeline for handling-time metrics (dashboard).</summary>
        [HttpGet("{orderId:int}/StatusHistory")]
        [ProducesResponseType(typeof(IApiResponse<List<OrderStatusHistoryEntryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetOrderStatusHistoryAsync(
            [FromRoute] int orderId,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.GetOrderStatusHistoryAsync(orderId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateOrderAsync([FromBody] CreateOrderReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.CreateOrderAsync(req, cancelToken));
        }

        [HttpPut("{orderId:int}")]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateOrderAsync([FromRoute] int orderId, [FromBody] UpdateOrderReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.UpdateOrderAsync(orderId, req, cancelToken));
        }

        [HttpPost("{orderId:int}/cancel")]
        [ProducesResponseType(typeof(IApiResponse<OrderRes?>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CancelOrderAsync([FromRoute] int orderId, [FromQuery] bool softDelete = true, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.CancelOrderAsync(orderId, softDelete, cancelToken));
        }

        /// <summary>Add items to an existing order (picking "הוסף פריט"). Body: { "items": [ CreateOrderItemReq, ... ] }.</summary>
        [HttpPost("{orderId:int}/Items")]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddItemsAsync([FromRoute] int orderId, [FromBody] AddOrderItemsReq? req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.AddItemsAsync(orderId, req?.Items ?? new(), cancelToken));
        }

        /// <summary>Remove a single order item (picking "הסר מוצר"). DELETE /Order/{orderId}/Items/{orderItemId}. Returns updated order.</summary>
        [HttpDelete("{orderId:int}/Items/{orderItemId:int}")]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> RemoveOrderItemAsync([FromRoute] int orderId, [FromRoute] int orderItemId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.RemoveOrderItemAsync(orderId, orderItemId, cancelToken));
        }

        /// <summary>Save picking state (שמור וצא). Body: { "items": [ { "orderItemId", "pickedQuantity", "totalPrice", "pickingUserConfirmed" }, ... ] }.</summary>
        [HttpPut("{orderId:int}/Picking")]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdatePickingAsync([FromRoute] int orderId, [FromBody] UpdatePickingReq? req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.UpdatePickingAsync(orderId, req, cancelToken));
        }

        /// <summary>Get customer profile by phone at site (for manual/phone order: name, manager note, last order date, order count, avg and total).</summary>
        [HttpGet("CustomerByPhone")]
        [ProducesResponseType(typeof(IApiResponse<CustomerProfileRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCustomerProfileByPhoneAsync([FromQuery] int siteId, [FromQuery] string? phone, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.GetCustomerProfileByPhoneAsync(siteId, phone, cancelToken));
        }

        /// <summary>Get last order line items by customer phone at site (for "last purchase" / רכישה אחרונה quick add).</summary>
        [HttpGet("LastOrderItems")]
        [ProducesResponseType(typeof(IApiResponse<List<OrderItemRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetLastOrderItemsAsync([FromQuery] int siteId, [FromQuery] string? phone, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.GetLastOrderItemsByPhoneAsync(siteId, phone, cancelToken));
        }

        /// <summary>Send reminder SMS to customer (שלח תזכורת). Uses OrderReady message template from account notification settings.</summary>
        [HttpPost("{orderId:int}/SendReminder")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SendReminderAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _orderSvc.SendReminderAsync(orderId, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_orderSvc);
        }
    }
}
