using George.Api.Core;
using George.Common;
using George.Data;
using George.Services.Partner;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Security.Cryptography;

namespace George.Api.Controllers
{
    /// <summary>
    /// Admin (JWT) endpoints for managing Partner API access per site: API key, outbound webhook, test ping.
    /// Secrets are returned exactly once (on generation) and never echoed afterwards.
    /// </summary>
    [Route("Partner/Admin", Name = "PartnerAdmin")]
    [ApiController]
    public class PartnerAdminController : GeorgeControllerBase
    {
        private readonly SiteStorage _siteStorage;
        private readonly PartnerWebhookDispatcher _webhooks;

        public PartnerAdminController(SiteStorage siteStorage, PartnerWebhookDispatcher webhooks, ILogger<PartnerAdminController> logger)
            : base(logger)
        {
            _siteStorage = siteStorage;
            _webhooks = webhooks;
        }

        /// <summary>Current Partner API configuration of a site (key installed? webhook URL? secret set?).</summary>
        [HttpGet("Settings")]
        [ProducesResponseType(typeof(IApiResponse<PartnerSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSettingsAsync([FromQuery] int siteId, CancellationToken cancelToken = default)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null)
                return NotFound();
            return Ok(new ApiResponse<PartnerSettingsRes> { Data = ToSettings(site) });
        }

        /// <summary>Generate a new Partner API key for a site. Returns the key once - copy it into the partner integration. Replaces any previous key.</summary>
        [HttpPost("GenerateSiteApiKey")]
        [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GenerateSiteApiKeyAsync([FromQuery] int siteId, CancellationToken cancelToken = default)
        {
            var keyBytes = new byte[32];
            RandomNumberGenerator.Fill(keyBytes);
            var apiKey = "pk_" + Convert.ToBase64String(keyBytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            var saved = await _siteStorage.SetPartnerApiKeyAsync(siteId, apiKey, cancelToken);
            if (saved == null)
                return NotFound();
            return Ok(new ApiResponse<object> { Data = new { apiKey = saved } });
        }

        /// <summary>Revoke the site's Partner API key (partner calls stop authenticating immediately).</summary>
        [HttpPost("RevokeSiteApiKey")]
        [ProducesResponseType(typeof(IApiResponse<object>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> RevokeSiteApiKeyAsync([FromQuery] int siteId, CancellationToken cancelToken = default)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null)
                return NotFound();
            await _siteStorage.SetPartnerApiKeyAsync(siteId, null, cancelToken);
            return Ok(new ApiResponse<object> { Data = new { revoked = true } });
        }

        /// <summary>Set (or clear, with an empty Url) the outbound order-events webhook of a site. Secret: null keeps, "" clears.</summary>
        [HttpPost("Webhook")]
        [ProducesResponseType(typeof(IApiResponse<PartnerSettingsRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> SetWebhookAsync([FromBody] PartnerWebhookReq req, CancellationToken cancelToken = default)
        {
            if (!string.IsNullOrWhiteSpace(req.Url)
                && (!Uri.TryCreate(req.Url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)))
                return Ok(new ApiResponse<PartnerSettingsRes>(George.Common.StatusCode.InvalidRequest, "Url must be an absolute http(s) URL."));
            var site = await _siteStorage.SetPartnerWebhookAsync(req.SiteId, req.Url, req.Secret, cancelToken);
            if (site == null)
                return NotFound();
            return Ok(new ApiResponse<PartnerSettingsRes> { Data = ToSettings(site) });
        }

        /// <summary>Send a signed <c>webhook.test</c> event to the site's webhook URL and report the HTTP outcome.</summary>
        [HttpPost("Webhook/Test")]
        [ProducesResponseType(typeof(IApiResponse<PartnerWebhookTestRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> TestWebhookAsync([FromQuery] int siteId, CancellationToken cancelToken = default)
        {
            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null)
                return NotFound();
            var res = await _webhooks.SendTestAsync(site, cancelToken);
            return Ok(new ApiResponse<PartnerWebhookTestRes> { Data = res });
        }

        private static PartnerSettingsRes ToSettings(DB.Site site) => new()
        {
            SiteId = site.Id,
            SiteName = site.SiteName,
            HasApiKey = !string.IsNullOrWhiteSpace(site.PartnerApiKey),
            ApiKeyPrefix = string.IsNullOrWhiteSpace(site.PartnerApiKey) ? null : site.PartnerApiKey!.Substring(0, Math.Min(8, site.PartnerApiKey.Length)) + "…",
            WebhookUrl = site.PartnerWebhookUrl,
            HasWebhookSecret = !string.IsNullOrWhiteSpace(site.PartnerWebhookSecret),
        };
    }
}
