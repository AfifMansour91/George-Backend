using George.Services.Marketing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace George.Api.Controllers;

/// <summary>
/// Incoming SMS-provider callbacks. No JWT - the URL carries a shared secret (<see cref="MarketingDeliveryReportService.WebhookSecret"/>),
/// and George itself hands that URL to the provider on every send, so nothing has to be pasted anywhere.
/// </summary>
[Route("Webhooks/Sms")]
[ApiController]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class SmsWebhookController : ControllerBase
{
    private readonly MarketingDeliveryReportService _reports;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmsWebhookController> _logger;

    public SmsWebhookController(MarketingDeliveryReportService reports, IConfiguration configuration, ILogger<SmsWebhookController> logger)
    {
        _reports = reports;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Inforu delivery reports (push DLR, JSON).</summary>
    [HttpPost("Inforu")]
    public async Task<IActionResult> InforuAsync([FromQuery(Name = "s")] string? secret, CancellationToken cancelToken)
    {
        if (!MarketingDeliveryReportService.IsValidSecret(_configuration, secret))
            return StatusCode(StatusCodes.Status403Forbidden);

        try
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(cancelToken).ConfigureAwait(false);
            await _reports.ApplyAsync(payload, cancelToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 200 on processing errors so the provider does not retry forever; the payload is in the log.
            _logger.LogError(ex, "Inforu DLR webhook processing failed.");
        }
        return Ok();
    }
}
