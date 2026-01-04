using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "GlobalCategory")]
    [ApiController]
    public class GlobalCategoryController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly GlobalCategoryService _globalCategorySvc;

        public GlobalCategoryController(GlobalCategoryService globalCategorySvc, ILogger<GlobalCategoryController> logger) : base(logger)
        {
            _globalCategorySvc = globalCategorySvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalCategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalCategoriesAsync(
            [FromQuery] ApiListReq<GlobalCategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.GetGlobalCategoriesAsync(request, cancelToken));
        }

        [HttpGet("{globalCategoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<GlobalCategoryRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalCategoryAsync([FromRoute] int globalCategoryId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.GetGlobalCategoryAsync(globalCategoryId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<GlobalCategoryRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateGlobalCategoryAsync([FromBody] CreateGlobalCategoryReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.CreateGlobalCategoryAsync(req, cancelToken));
        }

        [HttpPut("{globalCategoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<GlobalCategoryRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateGlobalCategoryAsync([FromRoute] int globalCategoryId, [FromBody] UpdateGlobalCategoryReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.UpdateGlobalCategoryAsync(globalCategoryId, req, cancelToken));
        }

        [HttpDelete("{globalCategoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteGlobalCategoryAsync([FromRoute] int globalCategoryId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.DeleteGlobalCategoryAsync(globalCategoryId, cancelToken));
        }

        [HttpGet("BusinessType/{businessTypeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalCategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalCategoriesByBusinessTypeAsync(
            [FromRoute] int businessTypeId,
            [FromQuery] ApiListReq<GlobalCategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new GlobalCategoryFilter();
            request.Filter.BusinessTypeId = businessTypeId;
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.GetGlobalCategoriesAsync(request, cancelToken));
        }

        [HttpGet("Parent/{parentGlobalCategoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalCategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalCategoriesByParentAsync(
            [FromRoute] int parentGlobalCategoryId,
            [FromQuery] ApiListReq<GlobalCategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new GlobalCategoryFilter();
            request.Filter.ParentGlobalCategoryId = parentGlobalCategoryId;
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.GetGlobalCategoriesAsync(request, cancelToken));
        }

        [HttpGet("Root")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<GlobalCategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetRootGlobalCategoriesAsync(
            [FromQuery] ApiListReq<GlobalCategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new GlobalCategoryFilter();
            request.Filter.ParentGlobalCategoryId = 0; // 0 means root categories (no parent)
            return await SafeCallWithErrorCatchingAsync(() => _globalCategorySvc.GetGlobalCategoriesAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_globalCategorySvc);
        }
    }
}

