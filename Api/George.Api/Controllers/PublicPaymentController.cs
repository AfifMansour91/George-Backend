using George.Common;
using George.Services.Payments;
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
}
