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
    [Route("[controller]", Name = "Category")]
    [ApiController]
    public class CategoryController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly CategoryService _categorySvc;

        public CategoryController(CategoryService categorySvc, ILogger<CategoryController> logger) : base(logger)
        {
            _categorySvc = categorySvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<CategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCategoriesAsync(
            [FromQuery] ApiListReq<CategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.GetCategoriesAsync(request, cancelToken));
        }

        [HttpGet("{categoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<CategoryRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCategoryAsync([FromRoute] int categoryId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.GetCategoryAsync(categoryId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<CategoryRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateCategoryAsync([FromBody] CreateCategoryReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.CreateCategoryAsync(req, cancelToken));
        }

        [HttpPut("{categoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<CategoryRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateCategoryAsync([FromRoute] int categoryId, [FromBody] UpdateCategoryReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.UpdateCategoryAsync(categoryId, req, cancelToken));
        }

        [HttpDelete("{categoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteCategoryAsync([FromRoute] int categoryId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.DeleteCategoryAsync(categoryId, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<CategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCategoriesByAccountAsync(
            [FromRoute] int accountId,
            [FromQuery] ApiListReq<CategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new CategoryFilter();
            request.Filter.AccountId = accountId;
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.GetCategoriesAsync(request, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<CategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCategoriesBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<CategoryFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new CategoryFilter();
            request.Filter.SiteId = siteId;
            return await SafeCallWithErrorCatchingAsync(() => _categorySvc.GetCategoriesAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_categorySvc);
        }
    }
}
