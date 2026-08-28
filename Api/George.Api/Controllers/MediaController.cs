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
        private readonly ThumbnailService _thumbSvc;
        private readonly ILogger<MediaController> _logger;

        public MediaController(MediaService mediaSvc, ThumbnailService thumbSvc, ILogger<MediaController> logger) : base(logger)
        {
            _mediaSvc = mediaSvc;
            _thumbSvc = thumbSvc;
            _logger = logger;
        }

        /// <summary>
        /// Serves a cached, resized JPEG of a George-hosted /files image (generated on first request;
        /// originals are never modified). Anonymous - img tags cannot send Bearer headers, and the
        /// underlying /files mount is public anyway. Falls back to redirecting to the original when the
        /// thumbnail cannot be produced (e.g. S3 storage mode or a missing file).
        /// </summary>
        [HttpGet("thumb")]
        [AllowAnonymous]
        public async Task<IActionResult> GetThumbAsync([FromQuery] string src, [FromQuery] int w = 400, CancellationToken cancelToken = default)
        {
            try
            {
                var (path, _) = await _thumbSvc.GetOrCreateThumbAsync(src, w, cancelToken);
                if (path != null)
                {
                    Response.Headers.CacheControl = "public, max-age=31536000, immutable";
                    return PhysicalFile(path, "image/jpeg");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Thumb generation failed for {Src} w={Width}", src, w);
            }

            // Only redirect to URLs that resolve inside OUR storage (never an open redirect).
            if (ThumbnailService.TryResolveOriginalPhysicalPath(src) != null)
                return Redirect(src);
            return NotFound();
        }

        /// <summary>
        /// Pre-generates thumbnails for all product images (background, own DI scope). Idempotent -
        /// already-cached sizes are skipped, so it is safe to trigger after bulk imports/uploads.
        /// </summary>
        [HttpPost("thumbs/warm")]
        public IActionResult WarmThumbs([FromServices] IServiceScopeFactory scopeFactory)
        {
            _ = Task.Run(async () =>
            {
                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ThumbnailService>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<MediaController>>();
                try
                {
                    await svc.WarmProductImageThumbsAsync(null, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Thumbnail cache warming failed");
                }
            });
            return Ok(new { started = true });
        }

        [HttpGet]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<MediaRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMediaAsync(
            [FromQuery] ApiListReq<MediaFilter> request,
            [FromQuery] bool? globalOnly,
            CancellationToken cancelToken = default)
        {
            if (globalOnly == true || (request.Filter?.GlobalOnly == true))
            {
                request.Filter ??= new MediaFilter();
                request.Filter.GlobalOnly = true;
                request.Filter.AccountId = null; // ensure we never mix with account filter
            }
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
        public async Task<IActionResult> DeleteMediaAsync([FromRoute] int mediaId, [FromQuery] int? accountId, [FromQuery] int? siteId, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.DeleteMediaAsync(mediaId, accountId, siteId, cancelToken));
        }

        [HttpPost("{mediaId:int}/Use")]
        [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> UseMediaAsync([FromRoute] int mediaId, [FromBody] UseMediaReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.UseMediaAsync(mediaId, req, cancelToken));
        }

        /// <summary>Download external media URLs and save files to our storage, then update media records.</summary>
        [HttpPost("DownloadToStorage")]
        [ProducesResponseType(typeof(IApiResponse<DownloadAndSaveMediaRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> DownloadAndSaveToStorageAsync([FromBody] DownloadAndSaveMediaReq req, CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.DownloadAndSaveToStorageAsync(req, cancelToken));
        }

        [HttpGet("Account/{accountId:int}")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<MediaRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetMediaByAccountAsync(
            [FromRoute] int accountId,
            [FromQuery] ApiListReq<MediaFilter> request,
            [FromQuery] int? siteId,
            CancellationToken cancelToken = default)
        {
            if (request.Filter == null) request.Filter = new MediaFilter();
            request.Filter.AccountId = accountId;
            if (siteId.HasValue) request.Filter.SiteId = siteId;
            return await SafeCallWithErrorCatchingAsync(() => _mediaSvc.GetMediaAsync(request, cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_mediaSvc);
        }
    }
}

