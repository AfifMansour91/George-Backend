using George.Services.Delivery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace George.Api.Controllers;

/// <summary>
/// Incoming delivery-provider status webhooks (courier assigned / out for delivery / delivered...).
/// No JWT - authenticated by the per-site webhook secret minted in DeliveryProviderConfig; the full
/// URL (incl. siteId + secret) is shown in the Integrations page for pasting at the provider console.
/// </summary>
[Route("Webhooks/Delivery")]
[ApiController]
[AllowAnonymous]
public class DeliveryWebhookController : ControllerBase
{
    private readonly DeliveryDispatchService _dispatchSvc;
    private readonly ILogger<DeliveryWebhookController> _logger;

    public DeliveryWebhookController(DeliveryDispatchService dispatchSvc, ILogger<DeliveryWebhookController> logger)
    {
        _dispatchSvc = dispatchSvc;
        _logger = logger;
    }

    [HttpPost("{providerKey}")]
    public async Task<IActionResult> PostAsync(
        [FromRoute] string providerKey,
        [FromQuery] int siteId,
        [FromQuery] string? secret,
        CancellationToken cancelToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync(cancelToken).ConfigureAwait(false);

            var result = await _dispatchSvc.ApplyWebhookAsync(providerKey, siteId, secret, payload, cancelToken)
                .ConfigureAwait(false);
            // Invalid secret / unknown provider gets 403 so a misconfigured URL is visible at the provider side.
            if (!result.IsSuccessful)
                return StatusCode(StatusCodes.Status403Forbidden);
        }
        catch (Exception ex)
        {
            // Return 200 for processing errors so the provider does not retry forever; details are logged.
            _logger.LogError(ex, "Delivery webhook processing failed (provider {Provider}, site {SiteId})", providerKey, siteId);
        }

        return Ok();
    }
}
