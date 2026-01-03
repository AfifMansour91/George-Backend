//using Azure.Core;
//using George.Api.Core;
//using George.Common;
//using George.Common.Request;
//using George.DB;
//using George.Services;
//using George.Services.Request;
//using George.Services.Response;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using System.Net;

//namespace George.Api.Controllers
//{
//    [Route("[controller]", Name = "Attribute")]
//    [ApiController]
//    public class AttributeController : GeorgeControllerBase, IAuthUserProvider
//    {
//        private readonly AttributeService _attributeSvc;

//        public AttributeController(AttributeService attributeSvc, ILogger<AttributeController> logger) : base(logger)
//        {
//            _attributeSvc = attributeSvc;
//        }

//        [AllowAnonymous]
//        [HttpGet]
//        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<AttributeRes>>), (int)HttpStatusCode.OK)]
//        public async Task<IActionResult> GetAttributesAsync(
//            [FromQuery] ApiListReq<AttributeFilter> request,
//            CancellationToken cancelToken = default)
//        {
//            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.GetAttributesAsync(request, cancelToken));
//        }

//        [HttpGet("{AttributeId:long}")]
//        [ProducesResponseType(typeof(IApiResponse<AttributeRes>), (int)HttpStatusCode.OK)]
//        public async Task<IActionResult> GetAttributeAsync([FromRoute] int attributeId, CancellationToken cancelToken = default)
//        {
//            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.GetAttributeAsync(attributeId, cancelToken));
//        }

//        [HttpPost]
//        [ProducesResponseType(typeof(IApiResponse<AttributeRes>), (int)HttpStatusCode.OK)]
//        public async Task<IActionResult> CreateAttributeAsync([FromBody] CreateAttributeReq req, CancellationToken cancelToken = default)
//        {
//            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.CreateAttributeAsync(req, cancelToken));
//        }

//        [HttpPut("{AttributeId:long}")]
//        [ProducesResponseType(typeof(IApiResponse<AttributeRes>), (int)HttpStatusCode.OK)]
//        public async Task<IActionResult> UpdateAttributeAsync([FromRoute] int attributeId, [FromBody] UpdateAttributeReq request, CancellationToken cancelToken = default)
//        {
//            if (attributeId != request.Id)
//                return CreateHttpResponse(Common.StatusCode.InvalidRequest, "Mismatching IDs.");

//            return await SafeCallWithErrorCatchingAsync(() => _attributeSvc.UpdateAttributeAsync(request, cancelToken));
//        }

//        [ApiExplorerSettings(IgnoreApi = true)]
//        public void SetAuthUser()
//        {
//            SetAuthUser(_attributeSvc);
//        }
//    }
//}
