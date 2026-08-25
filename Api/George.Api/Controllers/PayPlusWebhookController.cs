using System.Text.Json;
using George.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace George.Api.Controllers;

/// <summary>
/// PayPlus server-to-server callback (no JWT). Must be public HTTPS. Mirrors <see cref="CardcomWebhookController"/>:
/// always returns 200 to stop retry storms, extracts the correlating id defensively from query/form/JSON body.
///
/// Unlike Cardcom's webhook (which has no signature and instead re-validates by calling back into Cardcom with
/// credentials George already has), PayPlus signs its callback. Confirmed against official docs
/// (docs.payplus.co.il/reference/validate-requests-received-from-payplus): the `hash` request header holds
/// base64(HMAC-SHA256(JSON.stringify(request body), secret_key)), and the `user-agent` header must equal
/// "PayPlus". Resolving *which site's* secret key applies requires the order first (looked up by the same id
/// this controller resolves below), so the actual comparison happens in
/// <see cref="PaymentService.ProcessPayPlusWebhookAsync"/> once the order's site is known — this controller
/// only captures the raw body and the two headers and passes them through unverified. Every code path also
/// independently re-confirms the reported state via PayPlusGateway.InquireTransactionAsync before touching
/// money regardless of signature outcome, so this is defense-in-depth, not the only thing standing between a
/// spoofed request and a false capture.
/// </summary>
[Route("Webhooks/PayPlus")]
[ApiController]
[AllowAnonymous]
public class PayPlusWebhookController : ControllerBase
{
    private readonly PaymentService _paymentSvc;
    private readonly ILogger<PayPlusWebhookController> _logger;

    public PayPlusWebhookController(PaymentService paymentSvc, ILogger<PayPlusWebhookController> logger)
    {
        _paymentSvc = paymentSvc;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> PostAsync(CancellationToken cancelToken)
    {
        try
        {
            var rawBody = await ReadRequestBodyAsync(cancelToken).ConfigureAwait(false);
            var hashHeader = Request.Headers["hash"].FirstOrDefault();
            var userAgentHeader = Request.Headers["user-agent"].FirstOrDefault();

            var id = await ResolvePayPlusIdAsync(rawBody, cancelToken);
            if (!string.IsNullOrWhiteSpace(id))
                await _paymentSvc.ProcessPayPlusWebhookAsync(id, rawBody, hashHeader, userAgentHeader, cancelToken);
            else
                _logger.LogWarning("PayPlus webhook: could not resolve transaction_uid/page_request_uid from request.");
        }
        catch (Exception ex)
        {
            // Return 200 so PayPlus does not keep retrying; errors are logged for investigation.
            _logger.LogError(ex, "PayPlus webhook processing failed.");
        }

        return Ok();
    }

    private async Task<string?> ResolvePayPlusIdAsync(string? rawBody, CancellationToken cancelToken)
    {
        var fromQuery = Request.Query["transaction_uid"].FirstOrDefault()
            ?? Request.Query["page_request_uid"].FirstOrDefault()
            ?? Request.Query["more_info"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(fromQuery))
            return fromQuery.Trim();

        var contentType = (Request.ContentType ?? "").Trim();
        if (IsFormContentType(contentType))
        {
            var form = await Request.ReadFormAsync(cancelToken).ConfigureAwait(false);
            var fromForm = form["transaction_uid"].FirstOrDefault()
                ?? form["page_request_uid"].FirstOrDefault()
                ?? form["more_info"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(fromForm))
                return fromForm.Trim();
        }

        if (string.IsNullOrWhiteSpace(rawBody))
            return null;

        return ExtractIdFromJsonBody(rawBody);
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

    private string? ExtractIdFromJsonBody(string body)
    {
        var trimmed = body.Trim();
        if (trimmed.Length == 0 || !trimmed.StartsWith('{'))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            // PayPlus callback shape nests these under "data"; some flows may send them at the root.
            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                var fromData = FindStringProperty(data, "transaction_uid")
                    ?? FindStringProperty(data, "page_request_uid")
                    ?? FindStringProperty(data, "more_info");
                if (!string.IsNullOrWhiteSpace(fromData))
                    return fromData;
            }

            return FindStringProperty(root, "transaction_uid")
                ?? FindStringProperty(root, "page_request_uid")
                ?? FindStringProperty(root, "more_info");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "PayPlus webhook: invalid JSON body.");
            return null;
        }
    }

    private static string? FindStringProperty(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var prop))
            return null;
        var value = prop.ValueKind == JsonValueKind.String ? prop.GetString() : prop.GetRawText();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
    }
}
