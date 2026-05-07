using George.Api.Core;
using George.Common;
using George.Data;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "GlobalBrand")]
    [ApiController]
    public class GlobalBrandController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly GlobalBrandService _globalBrandSvc;

        public GlobalBrandController(GlobalBrandService globalBrandSvc, ILogger<GlobalBrandController> logger) : base(logger)
        {
            _globalBrandSvc = globalBrandSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalBrandRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalBrandsAsync(
            [FromQuery] ApiListReq<GlobalBrandFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.GetGlobalBrandsAsync(request, cancelToken));
        }

        [HttpGet("{globalBrandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<GlobalBrandRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalBrandAsync([FromRoute] int globalBrandId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.GetGlobalBrandAsync(globalBrandId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<GlobalBrandRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateGlobalBrandAsync([FromBody] CreateGlobalBrandReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.CreateGlobalBrandAsync(req, cancelToken));
        }

        [HttpPut("{globalBrandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<GlobalBrandRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateGlobalBrandAsync([FromRoute] int globalBrandId, [FromBody] UpdateGlobalBrandReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.UpdateGlobalBrandAsync(globalBrandId, req, cancelToken));
        }

        [HttpDelete("{globalBrandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteGlobalBrandAsync([FromRoute] int globalBrandId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.DeleteGlobalBrandAsync(globalBrandId, cancelToken));
        }

        [HttpGet("Parent/{parentGlobalBrandId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalBrandRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalBrandsByParentAsync(
            [FromRoute] int parentGlobalBrandId,
            [FromQuery] ApiListReq<GlobalBrandFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new GlobalBrandFilter();
            request.Filter.ParentGlobalBrandId = parentGlobalBrandId;
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.GetGlobalBrandsAsync(request, cancelToken));
        }

        [HttpGet("Root")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalBrandRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetRootGlobalBrandsAsync(
            [FromQuery] ApiListReq<GlobalBrandFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new GlobalBrandFilter();
            request.Filter.ParentGlobalBrandId = 0; // 0 = root
            return await SafeCallWithErrorCatchingAsync(() => _globalBrandSvc.GetGlobalBrandsAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_globalBrandSvc);
        }
    }
}
