using George.Api.Core;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Services;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "Catalog")]
    [ApiController]
    public class CatalogController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly CatalogService _svc;

        public CatalogController(CatalogService svc, ILogger<CatalogController> logger)
            : base(logger)
        {
            _svc = svc;
        }

        [HttpGet("Products")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<CatalogListRow>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCatalogProductsAsync([FromQuery] ApiListReq<CatalogProductListFilter> request, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetCatalogProductsAsync(request, cancelToken));
        }

        [HttpGet("Products/{templateId:long}")]
        [ProducesResponseType(typeof(IApiResponse<CatalogProductDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCatalogProductDetailAsync([FromRoute] long templateId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetCatalogProductDetailAsync(templateId, cancelToken));
        }

        [HttpPost("Products")]
        [ProducesResponseType(typeof(IApiResponse<CatalogProductDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateCatalogProductAsync([FromBody] CreateCatalogProductReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.CreateCatalogProductAsync(req, cancelToken));
        }

        [HttpPut("Products/{templateId:long}")]
        [ProducesResponseType(typeof(IApiResponse<CatalogProductDetailRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateCatalogProductAsync([FromRoute] long templateId, [FromBody] UpdateCatalogProductReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.UpdateCatalogProductAsync(templateId, req, cancelToken));
        }

        [HttpPut("Products/{templateId:long}/Status")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SetCatalogProductStatusAsync([FromRoute] long templateId, [FromBody] BoolReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.SetCatalogProductStatusAsync(templateId, req, cancelToken));
        }

        [HttpGet("Lookups")]
        [ProducesResponseType(typeof(IApiResponse<CatalogLookupsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetLookupsAsync(CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetLookupsAsync(cancelToken));
        }

        [HttpGet("Categories")]
        [ProducesResponseType(typeof(IApiResponse<List<GlobalCategoryNodeRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetGlobalCategoriesAsync(CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.GetGlobalCategoriesAsync(cancelToken));
        }

        [HttpPost("Categories")]
        [ProducesResponseType(typeof(IApiResponse<GlobalCategoryNodeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateGlobalCategoryAsync([FromBody] CreateGlobalCategoryReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.CreateGlobalCategoryAsync(req, cancelToken));
        }

        [HttpPut("Categories/{categoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<GlobalCategoryNodeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateGlobalCategoryAsync([FromRoute] int categoryId, [FromBody] UpdateGlobalCategoryReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _svc.UpdateGlobalCategoryAsync(categoryId, req, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_svc);
        }
    }
}
