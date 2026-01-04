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
    [Route("[controller]", Name = "Attribute")]
    [ApiController]
    public class AttributeController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly AttributeService _attributeSvc;

        public AttributeController(AttributeService attributeSvc, ILogger<AttributeController> logger) : base(logger)
        {
            _attributeSvc = attributeSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<AttributeRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAttributesAsync(
            [FromQuery] ApiListReq<AttributeFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.GetAttributesAsync(request, cancelToken));
        }

        [HttpGet("{attributeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<AttributeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAttributeAsync([FromRoute] int attributeId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.GetAttributeAsync(attributeId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<AttributeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateAttributeAsync([FromBody] CreateAttributeReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.CreateAttributeAsync(req, cancelToken));
        }

        [HttpPut("{attributeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<AttributeRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateAttributeAsync([FromRoute] int attributeId, [FromBody] UpdateAttributeReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.UpdateAttributeAsync(attributeId, req, cancelToken));
        }

        [HttpDelete("{attributeId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteAttributeAsync([FromRoute] int attributeId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.DeleteAttributeAsync(attributeId, cancelToken));
        }

        [HttpGet("Site/{siteId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<AttributeRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAttributesBySiteAsync(
            [FromRoute] int siteId,
            [FromQuery] ApiListReq<AttributeFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new AttributeFilter();
            if (request.Filter.SiteIds == null) request.Filter.SiteIds = new List<int>();
            request.Filter.SiteIds.Add(siteId);
            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.GetAttributesAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_attributeSvc);
        }
    }
}