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
    [Route("[controller]", Name = "Media")]
    [ApiController]
    public class MediaController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly MediaService _mediaSvc;

        public MediaController(MediaService mediaSvc, ILogger<MediaController> logger) : base(logger)
        {
            _mediaSvc = mediaSvc;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<MediaRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMediaAsync(
            [FromQuery] ApiListReq<MediaFilter> request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.GetMediaAsync(request, cancelToken));
        }

        [HttpGet("{mediaId:int}")]
        [ProducesResponseType(typeof(IApiResponse<MediaRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMediaAsync([FromRoute] int mediaId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.GetMediaAsync(mediaId, cancelToken));
        }

        [HttpPost]
        [ProducesResponseType(typeof(IApiResponse<MediaRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateMediaAsync([FromBody] CreateMediaReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.CreateMediaAsync(req, cancelToken));
        }

        [HttpPut("{mediaId:int}")]
        [ProducesResponseType(typeof(IApiResponse<MediaRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UpdateMediaAsync([FromRoute] int mediaId, [FromBody] UpdateMediaReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.UpdateMediaAsync(mediaId, req, cancelToken));
        }

        [HttpDelete("{mediaId:int}")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DeleteMediaAsync([FromRoute] int mediaId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.DeleteMediaAsync(mediaId, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<MediaRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMediaByAccountAsync(
            [FromRoute] int accountId,
            [FromQuery] ApiListReq<MediaFilter> request,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new MediaFilter();
            request.Filter.AccountId = accountId;
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.GetMediaAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_mediaSvc);
        }
    }
}

