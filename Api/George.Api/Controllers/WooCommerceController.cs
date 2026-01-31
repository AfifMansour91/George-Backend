using George.Api.Core;
using George.Common;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text;
using System.Text.Json;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "WooCommerce")]
    [ApiController]
    public class WooCommerceController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly WooCommerceService _wooCommerceService;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WooCommerceController(
            WooCommerceService wooCommerceService,
            ILogger<WooCommerceController> logger) : base(logger)
        {
            _wooCommerceService = wooCommerceService;
        }

        [HttpPost("Sync")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceSyncRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncToWooCommerceAsync(
            [FromBody] WooCommerceSyncReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _wooCommerceService.SyncToWooCommerceAsync(request, cancelToken));
        }

        /// <summary>Sync with streaming progress (NDJSON). Response: progress lines then one "done" line with result.</summary>
        [HttpPost("SyncStream")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncToWooCommerceStreamAsync(
            [FromBody] WooCommerceSyncReq request,
            CancellationToken cancelToken = default)
        {
            if (request == null)
            {
                return BadRequest();
            }

            Response.ContentType = "application/x-ndjson; charset=utf-8";
            Response.Headers.CacheControl = "no-cache";

            var streamLock = new object();
            void WriteLine(object obj)
            {
                lock (streamLock)
                {
                    var json = JsonSerializer.Serialize(obj, JsonOptions);
                    var line = json + "\n";
                    var bytes = Encoding.UTF8.GetBytes(line);
                    Response.Body.WriteAsync(bytes, 0, bytes.Length, cancelToken).GetAwaiter().GetResult();
                    Response.Body.FlushAsync(cancelToken).GetAwaiter().GetResult();
                }
            }

            try
            {
                var progress = new Progress<WooCommerceSyncProgress>(p =>
                {
                    WriteLine(new { type = "progress", total = p.Total, completed = p.Completed, failed = p.Failed });
                });

                var result = await _wooCommerceService.SyncToWooCommerceWithProgressAsync(request, progress, cancelToken);
                WriteLine(new { type = "done", message = result.Message, success = result.Success, failed = result.Failed });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WooCommerce SyncStream error");
                WriteLine(new { type = "error", message = ex.Message });
            }

            return new EmptyResult();
        }

        [HttpPost("SyncCategory")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceCategorySyncRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncCategoryToWooCommerceAsync(
            [FromBody] WooCommerceSyncCategoryReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _wooCommerceService.SyncCategoryToWooCommerceAsync(
                    request.CategoryId, 
                    request.SiteId, 
                    cancelToken));
        }

        [HttpPost("SyncAttribute")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceAttributeSyncRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncAttributeToWooCommerceAsync(
            [FromBody] WooCommerceSyncAttributeReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync(() =>
                _wooCommerceService.SyncAttributeToWooCommerceAsync(
                    request.AttributeId,
                    request.SiteId,
                    cancelToken));
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_wooCommerceService);
        }
    }
}

