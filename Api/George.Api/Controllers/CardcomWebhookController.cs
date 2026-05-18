using George.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace George.Api.Controllers;

/// <summary>Cardcom server-to-server webhook (no JWT). Must be public HTTPS.</summary>
[Route("Webhooks/Cardcom")]
[ApiController]
[AllowAnonymous]
public class CardcomWebhookController : ControllerBase
{
    private readonly PaymentService _paymentSvc;
    private readonly ILogger<CardcomWebhookController> _logger;

    public CardcomWebhookController(PaymentService paymentSvc, ILogger<CardcomWebhookController> logger)
    {
        _paymentSvc = paymentSvc;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> PostAsync(CancellationToken cancelToken)
    {
        var lowProfileId = Request.Query["lowprofilecode"].FirstOrDefault()
            ?? Request.Query["LowProfileId"].FirstOrDefault()
            ?? Request.Form["lowprofilecode"].FirstOrDefault()
            ?? Request.Form["LowProfileId"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(lowProfileId))
        {
            try
            {
                using var reader = new StreamReader(Request.Body);
                var body = await reader.ReadToEndAsync(cancelToken);
                if (body.Contains("LowProfileId", StringComparison.OrdinalIgnoreCase))
                {
                    var idx = body.IndexOf("LowProfileId", StringComparison.OrdinalIgnoreCase);
                    _logger.LogInformation("Cardcom webhook body: {Body}", body.Length > 500 ? body[..500] : body);
                }
            }
            catch { /* ignore */ }
        }

        if (!string.IsNullOrWhiteSpace(lowProfileId))
            await _paymentSvc.ProcessCardcomWebhookAsync(lowProfileId, cancelToken);

        return Ok();
    }
}
