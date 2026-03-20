using George.Api.Core;
using George.Common;
using George.Data;
using George.Services;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace George.Api.Controllers
{
    [Route("[controller]", Name = "WooCommerce")]
    [ApiController]
    public class WooCommerceController : GeorgeControllerBase, IAuthUserProvider
    {
        private readonly WooCommerceService _wooCommerceService;
        private readonly OrderService _orderService;
        private readonly SiteStorage _siteStorage;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public WooCommerceController(
            WooCommerceService wooCommerceService,
            OrderService orderService,
            SiteStorage siteStorage,
            ILogger<WooCommerceController> logger) : base(logger)
        {
            _wooCommerceService = wooCommerceService;
            _orderService = orderService;
            _siteStorage = siteStorage;
        }

        /// <summary>Generate a new WooCommerce API key for a site. Requires JWT. Returns the key once – copy it into the WooCommerce plugin.</summary>
        [HttpPost("GenerateSiteApiKey")]
        [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GenerateSiteApiKeyAsync([FromQuery] int siteId, CancellationToken cancelToken = default)
        {
            var keyBytes = new byte[32];
            RandomNumberGenerator.Fill(keyBytes);
            var apiKey = "wc_" + Convert.ToBase64String(keyBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var saved = await _siteStorage.SetInternalApiKeyAsync(siteId, apiKey, cancelToken);
            if (saved == null)
                return NotFound();
            return Ok(new ApiResponse<object> { Data = new { apiKey = saved } });
        }

        /// <summary>Verifies sync consistency: product counts per site, duplicate SKUs within a site, cross-site SKU overlap. Use to confirm no collision between branches.</summary>
        [HttpGet("VerifySync")]
        [ProducesResponseType(typeof(IApiResponse<WooCommerceSyncVerificationRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> VerifySyncAsync(
            [FromQuery] int accountId,
            [FromQuery] int? siteId = null,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync<WooCommerceSyncVerificationRes>(() =>
                _wooCommerceService.VerifySyncAsync(accountId, siteId, cancelToken));
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

        /// <summary>Syncs categories only and returns product IDs to sync. Client can then call Sync once per product (or batch) to avoid long-lived streams and QUIC errors.</summary>
        [HttpPost("SyncCategoriesAndGetProductIds")]
        [ProducesResponseType(typeof(IApiResponse<List<int>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SyncCategoriesAndGetProductIdsAsync(
            [FromBody] WooCommerceSyncReq request,
            CancellationToken cancelToken = default)
        {
            return await SafeCallWithErrorCatchingAsync<List<int>>(async () =>
            {
                var productIds = await _wooCommerceService.SyncCategoriesAndGetProductIdsAsync(request, cancelToken);
                return new ApiResponse<List<int>> { Data = productIds };
            });
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

            // Send first byte immediately to avoid HTTP 524 (Cloudflare/proxy timeout) while categories load and products are fetched
            WriteLine(new { type = "progress", total = 0, completed = 0, failed = 0 });

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

        /// <summary>Create or update order from WooCommerce (plugin calls when order is opened/edited). Auth: X-Api-Key or Bearer &lt;key&gt;. TEST: AllowAnonymous + payload.SiteId fallback enabled for Swagger; remove for production.</summary>
        [HttpPost("Order")]
        //[AllowAnonymous] // TEST ONLY: remove when done testing so only API key auth is allowed
        [Authorize(AuthenticationSchemes = WooCommerceApiKeyAuthenticationHandler.SchemeName)]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> CreateOrUpdateOrderAsync(
            [FromBody] WooCommerceOrderPayload payload,
            CancellationToken cancelToken = default)
        {
            var siteId = GetWooCommerceSiteId();

            ///////TEST ONLY
            // For testing without API key: use payload.SiteId when not authenticated
            //if (siteId == null && payload?.SiteId != null && int.TryParse(payload.SiteId.Trim(), out var parsedSiteId) && parsedSiteId > 0)
            //{
            //    var site = await _siteStorage.GetSiteAsync(parsedSiteId, cancelToken);
            //    if (site != null)
            //        siteId = parsedSiteId;
            //}
            ///////END TEST ONLY
            if (siteId == null)
                return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(async () =>
            {
                _logger.LogInformation(
                    "WooCommerce Order request received. siteId={SiteId}, orderNumber={OrderNumber}, headers={Headers}, payload={Payload}",
                    siteId.Value,
                    payload?.OrderNumber,
                    SerializeForLog(GetRequestHeadersForLog()),
                    SerializeForLog(payload));
                try
                {
                    var response = await _orderService.CreateOrUpdateOrderFromWooCommerceAsync(siteId.Value, payload!, cancelToken).ConfigureAwait(false);
                    _logger.LogInformation(
                        "WooCommerce Order response. siteId={SiteId}, orderNumber={OrderNumber}, response={Response}",
                        siteId.Value,
                        payload?.OrderNumber,
                        SerializeForLog(response));
                    return response;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "WooCommerce Order failed. siteId={SiteId}, orderNumber={OrderNumber}, headers={Headers}, payload={Payload}",
                        siteId.Value,
                        payload?.OrderNumber,
                        SerializeForLog(GetRequestHeadersForLog()),
                        SerializeForLog(payload));
                    throw;
                }
            });
        }

        /// <summary>Record payment from WooCommerce (invoice, clearance, paid-at). Auth: X-Api-Key or Bearer &lt;key&gt;.</summary>
        [HttpPost("OrderPayment")]
        [Authorize(AuthenticationSchemes = WooCommerceApiKeyAuthenticationHandler.SchemeName)]
        [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> RecordOrderPaymentAsync(
            [FromBody] WooCommerceOrderPaymentPayload payload,
            CancellationToken cancelToken = default)
        {
            var siteId = GetWooCommerceSiteId();
            if (siteId == null)
                return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _orderService.RecordPaymentFromWooCommerceAsync(siteId.Value, payload!, cancelToken));
        }

        private int? GetWooCommerceSiteId()
        {
            var sid = User.FindFirstValue(WooCommerceApiKeyAuthenticationHandler.ClaimSiteId);
            return int.TryParse(sid, out var id) ? id : null;
        }

        private Dictionary<string, string> GetRequestHeadersForLog()
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in Request.Headers)
            {
                var key = h.Key;
                var value = h.Value.ToString();
                if (key.Equals("X-Api-Key", StringComparison.OrdinalIgnoreCase) ||
                    key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    value = MaskSecret(value);
                }
                headers[key] = value;
            }
            return headers;
        }

        private static string MaskSecret(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var v = value.Trim();
            if (v.Length <= 12) return "****";
            return $"{v.Substring(0, 6)}****{v.Substring(v.Length - 4)}";
        }

        private static string SerializeForLog(object? data)
        {
            if (data == null) return "null";
            try
            {
                return JsonSerializer.Serialize(data, JsonOptions);
            }
            catch
            {
                return data.ToString() ?? "null";
            }
        }

        [ApiExplorerSettings(IgnoreApi = true)]
        public void SetAuthUser()
        {
            SetAuthUser(_wooCommerceService);
        }
    }
}

