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
    [Route("[controller]", Name = "TemplateProduct")]
    [ApiController]
    public class TemplateProductController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly TemplateProductService _templateProductSvc;

        public TemplateProductController(TemplateProductService templateProductSvc, ILogger<TemplateProductController> logger) : base(logger)
        {
            _templateProductSvc = templateProductSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<TemplateProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateProductsAsync(
            [FromQuery] ApiListReq<TemplateProductFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.GetTemplateProductsAsync(request, cancelToken));
        }

        [HttpGet("{templateProductId:int}")]
        [ProducesResponseType(typeof(IApiResponse<TemplateProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateProductAsync([FromRoute] int templateProductId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.GetTemplateProductAsync(templateProductId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<TemplateProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateTemplateProductAsync([FromBody] CreateTemplateProductReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.CreateTemplateProductAsync(req, cancelToken));
        }

        [HttpPut("{templateProductId:int}")]
        [ProducesResponseType(typeof(IApiResponse<TemplateProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateTemplateProductAsync([FromRoute] int templateProductId, [FromBody] UpdateTemplateProductReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.UpdateTemplateProductAsync(templateProductId, req, cancelToken));
        }

        [HttpDelete("{templateProductId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteTemplateProductAsync([FromRoute] int templateProductId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.DeleteTemplateProductAsync(templateProductId, cancelToken));
        }

        [HttpGet("Template/{templateId}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<TemplateProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateProductsByTemplateAsync(
            [FromRoute] string templateId,
            [FromQuery] ApiListReq<TemplateProductFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new TemplateProductFilter();
            request.Filter.TemplateId = templateId;
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.GetTemplateProductsAsync(request, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<TemplateProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateProductsBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<TemplateProductFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new TemplateProductFilter();
            request.Filter.SiteId = siteId;
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.GetTemplateProductsAsync(request, cancelToken));
        }

        [HttpGet("GlobalCategory/{globalCategoryId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<TemplateProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateProductsByGlobalCategoryAsync(
            [FromRoute] int globalCategoryId,
            [FromQuery] ApiListReq<TemplateProductFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new TemplateProductFilter();
            request.Filter.GlobalCategoryId = globalCategoryId;
            return await SafeCallWithErrorCatchingAsync(() => _templateProductSvc.GetTemplateProductsAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_templateProductSvc);
        }
    }
}

