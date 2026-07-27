using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Customer")]
    [ApiController]
    public class CustomerController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly CustomerService _customerSvc;

        public CustomerController(CustomerService customerSvc, ILogger<CustomerController> logger) : base(logger)
        {
            _customerSvc = customerSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<CustomerRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCustomersAsync(
            [FromQuery] int? siteId,
            [FromQuery] int? skip,
            [FromQuery] int? take,
            [FromQuery] string? search,
            [FromQuery] string? sortBy,
            [FromQuery] string? sortOrder,
            [FromQuery] string? phone,
            CancellationToken cancelToken = default)
        {
            var request = new ApiListReq<CustomerFilter>
            {
                Filter = new CustomerFilter
                {
                    SiteId = siteId,
                    Search = search,
                    SortBy = sortBy,
                    SortOrder = sortOrder,
                    Phone = phone
                },
                Skip = skip ?? 0,
                Take = take ?? 50,
                IncludeTotal = true
            };
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.GetCustomersAsync(request, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<CustomerDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateCustomerAsync([FromBody] CustomerCreateReq? req, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.CreateCustomerAsync(siteId, req ?? new CustomerCreateReq(), cancelToken));
        }

        /// <summary>Bulk import customers from a parsed spreadsheet (customers screen import button). Send rows in batches; each call returns counts + per-row issues.</summary>
        [HttpPost("Import")]
        [ProducesResponseType(typeof(IApiResponse<CustomerImportRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> ImportCustomersAsync([FromBody] CustomerImportReq? req, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.ImportCustomersAsync(siteId, req ?? new CustomerImportReq(), cancelToken));
        }

        [HttpGet("Stats")]
        [ProducesResponseType(typeof(IApiResponse<CustomerStatsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetStatsAsync([FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.GetStatsAsync(siteId, cancelToken));
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(IApiResponse<CustomerDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCustomerAsync([FromRoute] int id, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.GetCustomerAsync(id, siteId, cancelToken));
        }

        [HttpGet("{id:int}/LastOrder")]
        [ProducesResponseType(typeof(IApiResponse<CustomerLastOrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetLastOrderAsync([FromRoute] int id, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.GetLastOrderAsync(id, siteId, cancelToken));
        }

        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(IApiResponse<CustomerDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateCustomerAsync([FromRoute] int id, [FromBody] CustomerUpdateReq? req, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.UpdateCustomerAsync(id, siteId, req ?? new CustomerUpdateReq(), cancelToken));
        }

        [HttpPost("{id:int}/Activity")]
        [ProducesResponseType(typeof(IApiResponse<CustomerDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> AddActivityNoteAsync([FromRoute] int id, [FromBody] CustomerActivityNoteReq? req, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.AddActivityNoteAsync(id, siteId, req?.Text, cancelToken));
        }

        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteCustomerAsync([FromRoute] int id, [FromQuery] int? siteId, [FromQuery] bool withOrders = false, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.DeleteCustomerAsync(id, siteId, withOrders, cancelToken));
        }

        [HttpPost("DeleteMany")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteManyAsync([FromBody] DeleteManyCustomerReq? req, [FromQuery] int? siteId, [FromQuery] bool withOrders = false, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _customerSvc.DeleteManyAsync(req ?? new DeleteManyCustomerReq(), siteId, withOrders, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_customerSvc);
        }
    }
}
