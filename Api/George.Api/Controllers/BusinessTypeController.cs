using Azure.Core;
using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.DB;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "BusinessType")]
    [ApiController]
    public class BusinessTypeController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly BusinessTypeService _businessTypeSvc;

        public BusinessTypeController(BusinessTypeService businessTypeSvc, ILogger<BusinessTypeController> logger) : base(logger)
        {
            _businessTypeSvc = businessTypeSvc;
        }

        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<BusinessTypeRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBusinessTypesAsync(
            [FromQuery] ApiListReq<BusinessTypeFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _businessTypeSvc.GetBusinessTypesAsync(request, cancelToken));
        }

        [HttpGet("{BusinessTypeId:long}")]
        [ProducesResponseType(typeof(IApiResponse<BusinessTypeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetBusinessTypeAsync([FromRoute] int businessTypeId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _businessTypeSvc.GetBusinessTypeAsync(businessTypeId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<BusinessTypeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateBusinessTypeAsync([FromBody] CreateBusinessTypeReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _businessTypeSvc.CreateBusinessTypeAsync(req, cancelToken));
        }

        [HttpPut("{BusinessTypeId:long}")]
        [ProducesResponseType(typeof(IApiResponse<BusinessTypeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateBusinessTypeAsync([FromRoute] int businessTypeId, [FromBody] UpdateBusinessTypeReq request, CancellationToken cancelToken = default)
        {
            if (businessTypeId != request.Id)
                return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

            return await SafeCallWithErrorCatchingAsync(() => _businessTypeSvc.UpdateBusinessTypeAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_businessTypeSvc);
        }
    }
}
