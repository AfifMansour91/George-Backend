using George.Api.Core;
using George.Common;
using George.Services.Partner;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Security.Claims;

namespace George.Api.Controllers
{
    /// <summary>
    /// Partner API for external ordering integrations (e.g. the WhatsApp ordering agent).
    /// Auth: site <c>PartnerApiKey</c> via <c>X-Api-Key</c> or <c>Authorization: Bearer</c>.
    /// The site is always resolved from the key's claims - never from the request.
    /// Full contract for integrators: docs/PARTNER_API.md. Swagger: /swagger (document "partner").
    /// </summary>
    [Route("Partner/v1", Name = "Partner")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "partner")]
    [Authorize(AuthenticationSchemes = PartnerApiKeyAuthenticationHandler.SchemeName)]
    [EnableRateLimiting(George.Api.Core.StartupBase.PartnerRateLimitPolicy)]
    [ServiceFilter(typeof(PartnerRequestLogActionFilter))]
    public class PartnerController : GeorgeControllerBase
    {
        private readonly PartnerService _partnerService;

        public PartnerController(PartnerService partnerService, ILogger<PartnerController> logger)
            : base(logger)
        {
            _partnerService = partnerService;
        }

        //*************************    Store    *************************//

        /// <summary>Store facts: name, contact, pickup/delivery, delivery fees per city, payment methods, closed days.</summary>
        [HttpGet("Site")]
        [ProducesResponseType(typeof(IApiResponse<PartnerSiteRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetSiteAsync(CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() => _partnerService.GetSiteAsync(scope.Value.SiteId, cancelToken));
        }

        /// <summary>Supply dates that can be offered for pickup or delivery (closed days excluded), from today onward.</summary>
        [HttpGet("Availability")]
        [ProducesResponseType(typeof(IApiResponse<PartnerAvailabilityRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetAvailabilityAsync(
            [FromQuery] string deliveryType = "Pickup",
            [FromQuery] int days = 14,
            CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetAvailabilityAsync(scope.Value.SiteId, deliveryType, days, cancelToken));
        }

        //*************************    Catalog    *************************//

        /// <summary>Site catalog with the site's effective prices/stock. Hidden, site-excluded and bundle products are omitted.</summary>
        [HttpGet("Products")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<PartnerProductRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductsAsync(
            [FromQuery] string? search = null,
            [FromQuery] int? categoryId = null,
            [FromQuery] bool inStockOnly = false,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 50,
            CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetProductsAsync(scope.Value.SiteId, search, categoryId, inStockOnly, skip, take, cancelToken));
        }

        /// <summary>Single product with the site's effective values.</summary>
        [HttpGet("Products/{productId:int}")]
        [ProducesResponseType(typeof(IApiResponse<PartnerProductRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetProductAsync([FromRoute] int productId, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetProductAsync(scope.Value.SiteId, scope.Value.AccountId, productId, cancelToken));
        }

        /// <summary>Enabled categories of the site, ordered for display.</summary>
        [HttpGet("Categories")]
        [ProducesResponseType(typeof(IApiResponse<List<PartnerCategoryRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCategoriesAsync(CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetCategoriesAsync(scope.Value.SiteId, cancelToken));
        }

        //*************************    Customers    *************************//

        /// <summary>Customer lookup by phone (any format; normalized server-side). Found=false when unknown.</summary>
        [HttpGet("Customer")]
        [ProducesResponseType(typeof(IApiResponse<PartnerCustomerRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCustomerAsync([FromQuery] string phone, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetCustomerByPhoneAsync(scope.Value.SiteId, phone, cancelToken));
        }

        /// <summary>Items of the customer's most recent order at this site ("same as last time").</summary>
        [HttpGet("Customer/LastOrderItems")]
        [ProducesResponseType(typeof(IApiResponse<List<PartnerOrderItemRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetLastOrderItemsAsync([FromQuery] string phone, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetLastOrderItemsAsync(scope.Value.SiteId, phone, cancelToken));
        }

        /// <summary>The customer's orders at this site, newest first (all channels).</summary>
        [HttpGet("Customer/Orders")]
        [ProducesResponseType(typeof(IApiResponse<ApiListResponse<PartnerOrderRes>>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetCustomerOrdersAsync(
            [FromQuery] string phone,
            [FromQuery] int skip = 0,
            [FromQuery] int take = 10,
            CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetCustomerOrdersAsync(scope.Value.SiteId, phone, skip, take, cancelToken));
        }

        //*************************    Orders    *************************//

        /// <summary>Price a cart exactly as an order would be priced (promotions, coupon, delivery fee) without creating anything.</summary>
        [HttpPost("Orders/Quote")]
        [ProducesResponseType(typeof(IApiResponse<PartnerQuoteRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> QuoteAsync([FromBody] PartnerQuoteReq req, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.QuoteAsync(scope.Value.SiteId, scope.Value.AccountId, req, cancelToken));
        }

        /// <summary>Create an order. Prices are computed server-side from the catalog; PartnerRef makes retries idempotent.</summary>
        [HttpPost("Orders")]
        [ProducesResponseType(typeof(IApiResponse<PartnerOrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreateOrderAsync([FromBody] PartnerCreateOrderReq req, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.CreateOrderAsync(scope.Value.SiteId, scope.Value.AccountId, req, cancelToken));
        }

        /// <summary>Order status/details by George order id (must belong to the key's site).</summary>
        [HttpGet("Orders/{orderId:int}")]
        [ProducesResponseType(typeof(IApiResponse<PartnerOrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetOrderAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetOrderAsync(scope.Value.SiteId, orderId, cancelToken));
        }

        /// <summary>Order status/details by the partner's own reference (PartnerRef given at creation).</summary>
        [HttpGet("Orders/ByRef/{partnerRef}")]
        [ProducesResponseType(typeof(IApiResponse<PartnerOrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> GetOrderByRefAsync([FromRoute] string partnerRef, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.GetOrderByRefAsync(scope.Value.SiteId, partnerRef, cancelToken));
        }

        /// <summary>Cancel an order the partner placed while it is still New. Later states must be cancelled by the shop.</summary>
        [HttpPost("Orders/{orderId:int}/Cancel")]
        [ProducesResponseType(typeof(IApiResponse<PartnerOrderRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CancelOrderAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.CancelOrderAsync(scope.Value.SiteId, orderId, cancelToken));
        }

        /// <summary>Create a hosted payment page for the order and return its URL (send it to the customer in chat).</summary>
        [HttpPost("Orders/{orderId:int}/PaymentLink")]
        [ProducesResponseType(typeof(IApiResponse<PartnerPaymentLinkRes>), (int)HttpStatusCode.OK)]
        public async Task<IActionResult> CreatePaymentLinkAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
        {
            var scope = GetPartnerScope();
            if (scope == null) return Unauthorized();
            return await SafeCallWithErrorCatchingAsync(() =>
                _partnerService.CreatePaymentLinkAsync(scope.Value.SiteId, orderId, cancelToken));
        }

        private (int SiteId, int AccountId)? GetPartnerScope()
        {
            var sid = User.FindFirstValue(PartnerApiKeyAuthenticationHandler.ClaimSiteId);
            var aid = User.FindFirstValue(PartnerApiKeyAuthenticationHandler.ClaimAccountId);
            if (!int.TryParse(sid, out var siteId) || !int.TryParse(aid, out var accountId))
                return null;
            return (siteId, accountId);
        }
    }
}
