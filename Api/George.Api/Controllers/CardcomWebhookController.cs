using System.Text.Json;
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
        try
        {
            var lowProfileId = await ResolveLowProfileIdAsync(cancelToken);
            if (!string.IsNullOrWhiteSpace(lowProfileId))
                await _paymentSvc.ProcessCardcomWebhookAsync(lowProfileId, cancelToken);
            else
                _logger.LogWarning("Cardcom webhook: could not resolve LowProfileId from request.");
        }
        catch (Exception ex)
        {
            // Return 200 so Cardcom does not keep retrying; errors are logged for investigation.
            _logger.LogError(ex, "Cardcom webhook processing failed.");
        }

        return Ok();
    }

    private async Task<string?> ResolveLowProfileIdAsync(CancellationToken cancelToken)
    {
        var fromQuery = Request.Query["lowprofilecode"].FirstOrDefault()
            ?? Request.Query["LowProfileId"].FirstOrDefault()
            ?? Request.Query["LowProfileCode"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromQuery))
            return fromQuery.Trim();

        var contentType = (Request.ContentType ?? "").Trim();
        if (IsFormContentType(contentType))
        {
            var form = await Request.ReadFormAsync(cancelToken).ConfigureAwait(false);
            var fromForm = form["lowprofilecode"].FirstOrDefault()
                ?? form["LowProfileId"].FirstOrDefault()
                ?? form["LowProfileCode"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromForm))
                return fromForm.Trim();
        }

        var body = await ReadRequestBodyAsync(cancelToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(body))
            return null;

        return ExtractLowProfileIdFromBody(body);
    }

    private static bool IsFormContentType(string contentType) =>
        contentType.Contains("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase)
        || contentType.Contains("multipart/form-data", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ReadRequestBodyAsync(CancellationToken cancelToken)
    {
        if (Request.Body.CanSeek)
            Request.Body.Position = 0;
        else
            Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync(cancelToken).ConfigureAwait(false);
        if (Request.Body.CanSeek)
            Request.Body.Position = 0;
        return body;
    }

    private string? ExtractLowProfileIdFromBody(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return null;

                foreach (var name in new[] { "LowProfileId", "lowProfileId", "LowProfileCode", "lowprofilecode" })
                {
                    if (!root.TryGetProperty(name, out var prop))
                        continue;
                    var value = prop.ValueKind == JsonValueKind.String
                        ? prop.GetString()
                        : prop.GetRawText();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value.Trim().Trim('"');
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Cardcom webhook: invalid JSON body.");
            }
        }

        // Fallback: form-urlencoded body without Content-Type (legacy).
        foreach (var segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0) continue;
            var key = Uri.UnescapeDataString(segment[..eq]);
            if (!key.Equals("LowProfileId", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("lowprofilecode", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("LowProfileCode", StringComparison.OrdinalIgnoreCase))
                continue;
            var value = Uri.UnescapeDataString(segment[(eq + 1)..]);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        if (trimmed.Contains("LowProfileId", StringComparison.OrdinalIgnoreCase))
            _logger.LogInformation(
                "Cardcom webhook body without parseable LowProfileId (length {Length}).",
                trimmed.Length);

        return null;
    }
}
