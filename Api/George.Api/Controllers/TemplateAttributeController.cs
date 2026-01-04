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
    [Route("[controller]", Name = "TemplateAttribute")]
    [ApiController]
    public class TemplateAttributeController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly TemplateAttributeService _templateAttributeSvc;

        public TemplateAttributeController(TemplateAttributeService templateAttributeSvc, ILogger<TemplateAttributeController> logger) : base(logger)
        {
            _templateAttributeSvc = templateAttributeSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<TemplateAttributeRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateAttributesAsync(
            [FromQuery] ApiListReq<TemplateAttributeFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateAttributeSvc.GetTemplateAttributesAsync(request, cancelToken));
        }

        [HttpGet("{templateAttributeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<TemplateAttributeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateAttributeAsync([FromRoute] int templateAttributeId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateAttributeSvc.GetTemplateAttributeAsync(templateAttributeId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<TemplateAttributeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateTemplateAttributeAsync([FromBody] CreateTemplateAttributeReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateAttributeSvc.CreateTemplateAttributeAsync(req, cancelToken));
        }

        [HttpPut("{templateAttributeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<TemplateAttributeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateTemplateAttributeAsync([FromRoute] int templateAttributeId, [FromBody] UpdateTemplateAttributeReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateAttributeSvc.UpdateTemplateAttributeAsync(templateAttributeId, req, cancelToken));
        }

        [HttpDelete("{templateAttributeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteTemplateAttributeAsync([FromRoute] int templateAttributeId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _templateAttributeSvc.DeleteTemplateAttributeAsync(templateAttributeId, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<TemplateAttributeRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetTemplateAttributesBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<TemplateAttributeFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new TemplateAttributeFilter();
            request.Filter.SiteId = siteId;
            return await SafeCallWithErrorCatchingAsync(() => _templateAttributeSvc.GetTemplateAttributesAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_templateAttributeSvc);
        }
    }
}

