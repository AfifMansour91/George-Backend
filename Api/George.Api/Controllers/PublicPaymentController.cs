using George.Common;
using George.Services.Payments;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace George.Api.Controllers;

/// <summary>Public customer payment return (no login).</summary>
[Route("Public/Payment")]
[ApiController]
[AllowAnonymous]
public class PublicPaymentController : ControllerBase
{
    private readonly PaymentService _paymentSvc;

    public PublicPaymentController(PaymentService paymentSvc)
    {
        _paymentSvc = paymentSvc;
    }

    [HttpGet("Order/{orderId:int}/Return")]
    [ProducesResponseType(typeof(IApiResponse<OrderRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> ReturnAsync(
        [FromRoute] int orderId,
        [FromQuery] string? lowProfileId,
        CancellationToken cancelToken = default)
    {
        var result = await _paymentSvc.ApplyPaymentReturnAsync(orderId, lowProfileId, cancelToken);
        if (!result.IsSuccessful)
            return BadRequest(result);
        return Ok(result);
    }

    /// <summary>Giorgio payment link (/pay/{token}): what the landing page shows before redirecting to the gateway.</summary>
    [HttpGet("Link/{token}")]
    [ProducesResponseType(typeof(IApiResponse<PaymentLinkInfoRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> GetLinkAsync([FromRoute] string token, CancellationToken cancelToken = default)
    {
        var result = await _paymentSvc.GetPaymentLinkInfoAsync(token, cancelToken);
        if (!result.IsSuccessful)
            return result.StatusCode == George.Common.StatusCode.ItemNotFound ? NotFound(result) : BadRequest(result);
        return Ok(result);
    }

    /// <summary>Customer clicked "continue to payment": create a fresh gateway session and return its URL.</summary>
    [HttpPost("Link/{token}/Start")]
    [ProducesResponseType(typeof(IApiResponse<PaymentLinkStartRes>), (int)HttpStatusCode.OK)]
    public async Task<IActionResult> StartLinkAsync(
        [FromRoute] string token,
        [FromBody] StartPaymentLinkReq? req,
        CancellationToken cancelToken = default)
    {
        var appOrigin = Request.Headers.Origin.ToString();
        if (string.IsNullOrWhiteSpace(appOrigin)
            && Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer))
            appOrigin = referer.GetLeftPart(UriPartial.Authority);
        var result = await _paymentSvc.StartPaymentLinkAsync(token, req?.SaveCard, appOrigin, cancelToken);
        if (!result.IsSuccessful)
            return result.StatusCode == George.Common.StatusCode.ItemNotFound ? NotFound(result) : BadRequest(result);
        return Ok(result);
    }
}
