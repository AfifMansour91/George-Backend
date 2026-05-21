using George.Api.Core;
using George.Common;
using George.Services.Payments;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

[Route("[controller]", Name = "Payment")]
[ApiController]
public class PaymentController : GeorgeControllerBase, IAuthUserProvider
{
    private readonly PaymentService _paymentSvc;

    public PaymentController(PaymentService paymentSvc, ILogger<PaymentController> logger) : base(logger)
    {
        _paymentSvc = paymentSvc;
    }

    [HttpPost("Order/{orderId:int}/Session")]
    [ProducesResponseType(typeof(IApiResponse<PaymentSessionRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> CreateSessionAsync(
        [FromRoute] int orderId,
        [FromQuery] string? channel,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.CreatePaymentSessionAsync(orderId, channel, cancelToken));
    }

    [HttpPost("Order/{orderId:int}/SendSms")]
    [ProducesResponseType(typeof(IApiResponse<SendPaymentSmsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendSmsAsync(
        [FromRoute] int orderId,
        [FromBody] SendPaymentSmsReq? req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _paymentSvc.SendPaymentSmsAsync(orderId, req?.OverridePhone, cancelToken));
    }

    [HttpPost("Order/{orderId:int}/Finalize")]
    [ProducesResponseType(typeof(IApiResponse<FinalizePickingPaymentRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> FinalizeAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.FinalizePickingPaymentAsync(orderId, cancelToken));
    }

    [HttpPost("Order/{orderId:int}/Invoice/Issue")]
    [ProducesResponseType(typeof(IApiResponse<OrderInvoiceRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> IssueInvoiceAsync(
        [FromRoute] int orderId,
        [FromQuery] bool sendEmail = false,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _paymentSvc.IssueOrderInvoiceAsync(orderId, sendByEmail: sendEmail, cancelToken: cancelToken));
    }

    [HttpPost("Order/{orderId:int}/Invoice/Send")]
    [ProducesResponseType(typeof(IApiResponse<OrderInvoiceRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendInvoiceAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.SendOrderInvoiceAsync(orderId, cancelToken));
    }

    [HttpPost("Order/{orderId:int}/Invoice/SendSms")]
    [ProducesResponseType(typeof(IApiResponse<OrderInvoiceRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> SendInvoiceSmsAsync(
        [FromRoute] int orderId,
        [FromBody] SendPaymentSmsReq? req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _paymentSvc.SendOrderInvoiceSmsAsync(orderId, req?.OverridePhone, cancelToken));
    }

    [HttpPost("Order/{orderId:int}/Void")]
    [ProducesResponseType(typeof(IApiResponse<bool>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> VoidPendingAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.VoidPendingPaymentAsync(orderId, cancelToken));
    }

    [HttpPost("Order/{orderId:int}/Refund")]
    [ProducesResponseType(typeof(IApiResponse<RefundPaymentRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> RefundAsync(
        [FromRoute] int orderId,
        [FromBody] RefundPaymentReq req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.RefundOrderAsync(orderId, req, cancelToken));
    }

    [HttpGet("Order/{orderId:int}/Events")]
    [ProducesResponseType(typeof(IApiResponse<List<PaymentEventRes>>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetEventsAsync([FromRoute] int orderId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.GetPaymentEventsAsync(orderId, cancelToken));
    }

    [HttpGet("Site/{siteId:int}/SavedCard")]
    [ProducesResponseType(typeof(IApiResponse<SavedCardRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSavedCardAsync(
        [FromRoute] int siteId,
        [FromQuery] string? phone,
        [FromQuery] int? customerId = null,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _paymentSvc.GetSavedCardForCustomerAsync(siteId, phone, customerId, cancelToken));
    }

    [HttpGet("Site/{siteId:int}/Settings")]
    [ProducesResponseType(typeof(IApiResponse<SitePaymentSettingsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetSiteSettingsAsync([FromRoute] int siteId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.GetSitePaymentSettingsAsync(siteId, cancelToken));
    }

    [HttpPut("Site/{siteId:int}/Settings")]
    [ProducesResponseType(typeof(IApiResponse<SitePaymentSettingsRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> UpdateSiteSettingsAsync(
        [FromRoute] int siteId,
        [FromBody] UpdateSitePaymentSettingsReq req,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.UpdateSitePaymentSettingsAsync(siteId, req, cancelToken));
    }

    [HttpPost("Site/{siteId:int}/TestConnection")]
    [ProducesResponseType(typeof(IApiResponse<TestConnectionRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> TestConnectionAsync([FromRoute] int siteId, CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() => _paymentSvc.TestConnectionAsync(siteId, cancelToken));
    }

    /// <summary>Backfill Last4Digits/CardBrand/TokenExDate on saved cards from OrderPaymentEvent JSON.</summary>
    [HttpPost("Site/{siteId:int}/BackfillSavedCardDisplay")]
    [ProducesResponseType(typeof(IApiResponse<int>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> BackfillSavedCardDisplayAsync(
        [FromRoute] int siteId,
        CancellationToken cancelToken = default)
    {
        return await SafeCallWithErrorCatchingAsync(() =>
            _paymentSvc.BackfillSavedCardDisplayAsync(siteId, cancelToken));
    }

    [ApiExplorerSettings(IgnoreApi = true)]
    public void SetAuthUser()
    {
        SetAuthUser(_paymentSvc);
    }
}
