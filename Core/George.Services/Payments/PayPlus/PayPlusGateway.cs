using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using George.Common.Payment;
using George.Services.Payments;
using Microsoft.Extensions.Logging;

namespace George.Services.Payments.PayPlus;

/// <summary>
/// PayPlus gateway - second <see cref="IPaymentGatewayProvider"/> implementation alongside Cardcom, built to
/// the same "Giorgio owns capture" behavior: the hosted page places an authorization only (charge_method=2,
/// PayPlus's own J5 terminology), the actual charge happens at picking via
/// <see cref="CaptureAuthorizationAsync"/>. Unlike Cardcom, PayPlus captures the SAME transaction_uid the
/// authorization returned - there is no separate reusable token / approval-number pair to track.
///
/// Endpoints and field names below are confirmed against https://docs.payplus.co.il (not against a live
/// sandbox exchange) - see docs/PayPlus-test-site-setup.md for the end-to-end verification this still needs
/// before relying on it in production. Apple Pay / Google Pay are intentionally NOT implemented here: per
/// the agreed scope they are tabs on PayPlus's own hosted page (dashboard-configured), invisible to this
/// gateway, exactly like Cardcom's wallet handling today.
/// </summary>
/// <summary>Saved-card token fields extracted from a PayPlus IPN/callback payload.</summary>
public sealed class PayPlusSavedTokenFields
{
    public string? Token { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string? Last4Digits { get; init; }
    public string? CardBrand { get; init; }
}

public sealed class PayPlusGateway : IPaymentGatewayProvider
{
    public const string HttpClientName = "PayPlusApi";
    private const string ProdBaseUrl = "https://restapi.payplus.co.il/api/v1.0/";
    private const string SandboxBaseUrl = "https://restapidev.payplus.co.il/api/v1.0/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PayPlusGateway> _logger;

    public PayPlusGateway(IHttpClientFactory httpClientFactory, ILogger<PayPlusGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderId => PaymentGatewayProviderId.PayPlus;

    public PaymentGatewayCapabilities Capabilities { get; } = new()
    {
        SupportsHostedSession = true,
        SupportsTokenCharge = true,
        SupportsCaptureAuthorization = true,
        SupportsPartialRefund = true,
        SupportsVoidAuthorization = true,
        SupportsMotoPortal = false,
        SupportsCaptureAboveAuth = false,
    };

    public async Task<CreateHostedSessionResult> CreateHostedSessionAsync(
        SitePaymentCredentials credentials,
        CreateHostedSessionRequest request,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.PaymentPageUid) || string.IsNullOrWhiteSpace(credentials.ApiName))
            return FailCreate("config", "PayPlus payment page UID or API key is not configured.");

        var chargeMethod = request.UseAuthorizationHold ? 2 : 1; // PayPlus: 2=Approval(J5), 1=Charge(J4)

        var customer = new Dictionary<string, object?>
        {
            ["customer_name"] = string.IsNullOrWhiteSpace(request.CustomerName) ? "Customer" : request.CustomerName,
            ["email"] = request.CustomerEmail,
            ["phone"] = request.CustomerPhone,
        };

        var body = new Dictionary<string, object?>
        {
            ["payment_page_uid"] = credentials.PaymentPageUid,
            ["charge_method"] = chargeMethod,
            ["amount"] = request.Amount,
            ["currency_code"] = credentials.Currency,
            ["more_info"] = request.ReturnValue,
            ["language_code"] = string.IsNullOrWhiteSpace(request.Language) ? "he" : request.Language.Trim(),
            ["create_token"] = request.SaveCard,
            ["refURL_success"] = request.SuccessRedirectUrl,
            ["refURL_failure"] = request.FailedRedirectUrl,
            ["refURL_callback"] = request.WebHookUrl,
            ["customer"] = customer,
        };

        // Installments apply only to immediate charges; holds are always single-payment (mirrors Cardcom).
        if (!request.UseAuthorizationHold && request.MaxInstallments > 1)
            body["payments"] = Math.Clamp(request.MaxInstallments, 1, 36);

        var json = await PostJsonAsync(credentials, "PaymentPages/generateLink", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return FailCreate("http", "Empty response from PayPlus.");

        if (!IsResultsSuccess(json))
            return FailCreate(GetResultsCode(json)?.ToString() ?? "error", GetResultsDescription(json) ?? "Create failed", json);

        return new CreateHostedSessionResult
        {
            Success = true,
            PaymentUrl = GetDataString(json, "payment_page_link"),
            LowProfileId = GetDataString(json, "page_request_uid"),
            RawJson = json,
        };
    }

    public async Task<ValidateCallbackResult> ValidateCallbackAsync(
        SitePaymentCredentials credentials,
        ValidateCallbackRequest request,
        CancellationToken cancelToken = default)
    {
        // request.LowProfileId is reused across gateways as "the opaque session id to validate" -
        // for PayPlus that's the page_request_uid (PRUID) returned by generateLink.
        var body = new Dictionary<string, object?> { ["payment_request_uid"] = request.LowProfileId };

        var json = await PostJsonAsync(credentials, "PaymentPages/ipn", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return new ValidateCallbackResult { Success = false, ResponseCode = -1, Description = "Empty response" };

        try
        {
            return MapTransactionResult(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPlus ipn parse failed for page_request_uid {PageRequestUid}", request.LowProfileId);
            return new ValidateCallbackResult { Success = false, ResponseCode = -1, Description = ex.Message, RawJson = json };
        }
    }

    public async Task<PaymentTransactionResult> CaptureAuthorizationAsync(
        SitePaymentCredentials credentials,
        CaptureAuthorizationRequest request,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId))
            return Fail("PayPlus capture requires the authorization's transaction_uid.");

        var body = new Dictionary<string, object?>
        {
            ["transaction_uid"] = request.ProviderTransactionId,
            ["amount"] = request.Amount,
            ["more_info"] = request.ExternalUniqTranId,
        };

        var json = await PostJsonAsync(credentials, "Transactions/ChargeByTransactionUID", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return Fail("Empty response from PayPlus.");

        // Unlike Cardcom (which embeds document creation in the same Transactions/Transaction call), PayPlus's
        // Invoice+ document API is a separate endpoint (books/docs/new/) with its own line-item shape - the
        // caller creates the document explicitly via CreateDocumentAsync after a successful capture, using
        // the full order (line items, address) rather than the CardOwner-only fields available here.
        return MapTransactionalResult(json);
    }

    /// <summary>
    /// Reuses a saved PayPlus token to place a hold (analogue of Cardcom's phone-order J5 reuse).
    /// UNCONFIRMED against the sandbox: PayPlus's documented flow for a saved token still goes through
    /// PaymentPages/generateLink (returning a page link), which may or may not settle synchronously when
    /// a token is supplied. Verify before relying on this for phone/manual orders.
    /// </summary>
    public async Task<PaymentTransactionResult> PlaceTokenAuthorizationHoldAsync(
        SitePaymentCredentials credentials,
        PlaceTokenAuthorizationHoldRequest request,
        CancellationToken cancelToken = default)
        => await ChargeOrHoldByTokenAsync(credentials, request.Amount, request.Token, chargeMethod: 2,
            request.ExternalUniqTranId, cancelToken).ConfigureAwait(false);

    /// <summary>Same caveat as <see cref="PlaceTokenAuthorizationHoldAsync"/> - verify against the sandbox.</summary>
    public async Task<PaymentTransactionResult> ChargeTokenAsync(
        SitePaymentCredentials credentials,
        ChargeTokenRequest request,
        CancellationToken cancelToken = default)
        => await ChargeOrHoldByTokenAsync(credentials, request.Amount, request.Token, chargeMethod: 1,
            request.ExternalUniqTranId, cancelToken).ConfigureAwait(false);

    private async Task<PaymentTransactionResult> ChargeOrHoldByTokenAsync(
        SitePaymentCredentials credentials,
        decimal amount,
        string token,
        int chargeMethod,
        string externalUniqTranId,
        CancellationToken cancelToken)
    {
        if (string.IsNullOrWhiteSpace(credentials.PaymentPageUid))
            return Fail("PayPlus payment page UID is not configured.");

        var body = new Dictionary<string, object?>
        {
            ["payment_page_uid"] = credentials.PaymentPageUid,
            ["charge_method"] = chargeMethod,
            ["amount"] = amount,
            ["currency_code"] = credentials.Currency,
            ["more_info"] = externalUniqTranId,
            ["token"] = token,
        };

        var json = await PostJsonAsync(credentials, "PaymentPages/generateLink", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return Fail("Empty response from PayPlus.");

        if (!IsResultsSuccess(json))
            return Fail(GetResultsDescription(json) ?? "PayPlus token charge failed.", json);

        // A synchronous charge/hold via token returns transaction data directly; a page link with no
        // transaction_uid means PayPlus still expects a redirect, which this (server-side, no browser)
        // flow cannot follow - surface that clearly instead of silently reporting success.
        var txId = GetDataString(json, "transaction_uid");
        if (string.IsNullOrWhiteSpace(txId))
            return Fail("PayPlus returned a hosted-page link instead of a synchronous result for the saved token - this flow needs sandbox verification.", json);

        return MapTransactionalResult(json);
    }

    public async Task<PaymentTransactionResult> RefundAsync(
        SitePaymentCredentials credentials,
        RefundRequest request,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.OriginalTranzactionId))
            return Fail("PayPlus refund requires the transaction_uid being refunded.");

        var body = new Dictionary<string, object?>
        {
            ["transaction_uid"] = request.OriginalTranzactionId,
            ["amount"] = request.Amount,
            ["more_info"] = request.ExternalUniqTranId,
        };

        var json = await PostJsonAsync(credentials, "Transactions/RefundByTransactionUID", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return Fail("Empty response from PayPlus.");

        return MapTransactionalResult(json);
    }

    public async Task<PaymentTransactionResult> VoidAuthorizationAsync(
        SitePaymentCredentials credentials,
        VoidAuthorizationRequest request,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProviderTransactionId))
            return Fail("PayPlus void requires the transaction_uid to cancel.");

        // terminal_uid/cashier_uid are documented as part of this call but have no analogue in our
        // per-site credential model (they're PayPlus POS/device concepts) - omitted here. If PayPlus
        // rejects the call without them for a given account, this needs a credential-model addition;
        // verify against the sandbox (see docs/PayPlus-test-site-setup.md).
        var body = new Dictionary<string, object?> { ["transaction_uid"] = request.ProviderTransactionId };

        var json = await PostJsonAsync(credentials, "Transactions/Cancel", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return Fail("Empty response from PayPlus.");

        return MapTransactionalResult(json);
    }

    public async Task<TestConnectionResult> TestConnectionAsync(
        SitePaymentCredentials credentials,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiName) || string.IsNullOrWhiteSpace(credentials.ApiPassword))
            return new TestConnectionResult { Success = false, Message = "PayPlus API key and secret key are required." };

        // Minimal validation: View with a dummy id returns a structured (not transport-level) error if
        // credentials are valid - mirrors Cardcom's TestConnectionAsync approach.
        var body = new Dictionary<string, object?> { ["transaction_uid"] = "00000000-0000-0000-0000-000000000000" };
        var json = await PostJsonAsync(credentials, "Transactions/View", body, cancelToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return new TestConnectionResult { Success = false, Message = "No response from PayPlus." };

        // An auth failure (bad api-key/secret-key) surfaces as an HTTP-level rejection before results.code
        // is even meaningful; PostJsonAsync logs the raw body on non-2xx - a structured results object
        // (even an error one) here means the request reached PayPlus and was evaluated with these credentials.
        var hasResults = TryGetObjectProperty(json, "results", out _);
        return hasResults
            ? new TestConnectionResult { Success = true, Message = $"PayPlus reachable ({GetResultsDescription(json)})." }
            : new TestConnectionResult { Success = false, Message = "Unexpected response from PayPlus." };
    }

    /// <summary>PayPlus Transactions/View - analogue of Cardcom's GetTransactionInfoById. Not on the shared
    /// interface (mirrors how Cardcom's equivalent is also a gateway-specific extra method); returns the
    /// same <see cref="CardcomTransactionInfoResult"/> shape so <see cref="GatewayChargeVerification"/>'s
    /// comparison rules apply unchanged to either provider.</summary>
    public async Task<CardcomTransactionInfoResult> InquireTransactionAsync(
        SitePaymentCredentials credentials,
        string transactionUid,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiName) || string.IsNullOrWhiteSpace(credentials.ApiPassword))
            return new CardcomTransactionInfoResult { Success = false, ResponseCode = -1, Description = "PayPlus credentials are not configured." };

        var body = new Dictionary<string, object?> { ["transaction_uid"] = transactionUid };
        var json = await PostJsonAsync(credentials, "Transactions/View", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return new CardcomTransactionInfoResult { Success = false, ResponseCode = -1, Description = "Empty response from PayPlus." };

        return ParseTransactionInfoResult(json);
    }

    /// <summary>PayPlus PaymentPages/ipn - looks up what happened to a hosted-page session by its
    /// page_request_uid (the only id we hold before any webhook/return arrives). Same defensive role as
    /// Cardcom's GetLpResult in the return flow: the redirect back is never trusted by itself.</summary>
    public async Task<CardcomTransactionInfoResult> InquirePageRequestAsync(
        SitePaymentCredentials credentials,
        string pageRequestUid,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiName) || string.IsNullOrWhiteSpace(credentials.ApiPassword))
            return new CardcomTransactionInfoResult { Success = false, ResponseCode = -1, Description = "PayPlus credentials are not configured." };

        var body = new Dictionary<string, object?> { ["payment_request_uid"] = pageRequestUid };
        var json = await PostJsonAsync(credentials, "PaymentPages/ipn", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return new CardcomTransactionInfoResult { Success = false, ResponseCode = -1, Description = "Empty response from PayPlus." };

        return ParseIpnResult(json);
    }

    /// <summary>IPN responses carry the transaction under different field names than Transactions/View:
    /// `type` (not `transaction_type`) plus a `status`/`status_code` pair ("approved"/"000").</summary>
    internal static CardcomTransactionInfoResult ParseIpnResult(string json)
    {
        if (!IsResultsSuccess(json))
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = GetResultsCode(json) ?? -1,
                Description = GetResultsDescription(json) ?? "PayPlus IPN inquiry failed.",
                RawJson = json,
            };
        }

        var statusCode = GetDataString(json, "status_code");
        var status = GetDataString(json, "status");
        var approved = string.Equals(statusCode, "000", StringComparison.Ordinal)
            || string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase);
        if (!approved)
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = GetResultsCode(json) ?? -1,
                Description = GetDataString(json, "status_description") ?? status ?? "PayPlus transaction not approved.",
                RawJson = json,
            };
        }

        var type = GetDataString(json, "type"); // e.g. "Charge" / "Approval" / "Check"
        var isHold = type is "Approval" or "Check";
        var isRefund = type is "Refund" or "Cancel";

        return new CardcomTransactionInfoResult
        {
            Success = true,
            ResponseCode = 0,
            Description = GetDataString(json, "status_description"),
            TranzactionId = GetDataString(json, "transaction_uid"),
            Amount = GetDataDecimal(json, "amount"),
            DealType = type,
            IsRefund = isRefund,
            RawJson = json,
            IsFinalCharge = !isHold && !isRefund && type is not null,
            IsAuthorizationHold = isHold,
        };
    }

    internal static CardcomTransactionInfoResult ParseTransactionInfoResult(string json)
    {
        if (!IsResultsSuccess(json))
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = GetResultsCode(json) ?? -1,
                Description = GetResultsDescription(json) ?? "PayPlus transaction inquiry failed.",
                RawJson = json,
            };
        }

        // A found-but-declined transaction: the inquiry itself succeeded, but the transaction must not
        // count as charged/held (mirrors Cardcom's non-zero ResponseCode semantics).
        var statusCode = GetTransactionString(json, "status_code");
        if (statusCode != null && !string.Equals(statusCode, "000", StringComparison.Ordinal))
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = int.TryParse(statusCode, out var sc) ? sc : -1,
                Description = GetTransactionString(json, "status_description")
                    ?? GetResultsDescription(json) ?? "PayPlus transaction not approved.",
                TranzactionId = GetTransactionString(json, "transaction_uid"),
                Amount = GetTransactionDecimal(json, "amount"),
                RawJson = json,
            };
        }

        var transactionType = GetTransactionString(json, "transaction_type"); // e.g. "Charge" / "Approval" / "Check"
        var isHold = transactionType is "Approval" or "Check";
        var isRefund = transactionType is "Refund" or "Cancel";
        // A voided/cancelled transaction must never read as a live charge or hold.
        var isCancelled = string.Equals(GetTransactionString(json, "transaction_is_cancelled"), "true", StringComparison.OrdinalIgnoreCase);
        var isFinalCharge = !isHold && !isRefund && !isCancelled && transactionType is not null;
        var amount = GetTransactionDecimal(json, "amount");
        var txId = GetTransactionString(json, "transaction_uid");

        return new CardcomTransactionInfoResult
        {
            Success = true,
            ResponseCode = GetResultsCode(json) ?? 0,
            Description = GetResultsDescription(json),
            TranzactionId = txId,
            Amount = amount,
            DealType = isCancelled ? $"{transactionType} (cancelled)" : transactionType,
            IsRefund = isRefund,
            RawJson = json,
            IsFinalCharge = isFinalCharge,
            IsAuthorizationHold = isHold && !isCancelled,
        };
    }

    /// <summary>PayPlus Invoice+ document creation - not on the shared interface, mirrors Cardcom's
    /// CreateDocumentAsync (also a gateway-specific extra method).</summary>
    public async Task<PaymentTransactionResult> CreateDocumentAsync(
        SitePaymentCredentials credentials,
        CreatePayPlusDocumentRequest request,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiName) || string.IsNullOrWhiteSpace(credentials.ApiPassword))
            return Fail("PayPlus API key/secret key are required to issue documents.");

        var body = PayPlusDocumentPayload.ToDictionary(request.Document);
        var doctype = string.IsNullOrWhiteSpace(request.Document.DocType) ? "inv_tax_receipt" : request.Document.DocType;

        var json = await PostJsonAsync(credentials, $"books/docs/new/{doctype}", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return Fail("Empty response from PayPlus.");

        // Failures come back root-level ({"status":"failure","error":"brand-not-found",...}), not wrapped
        // in the usual {results:{...}} envelope - surface the actual error code, not a generic message.
        if (!TryGetObjectProperty(json, "docUID", out _) && !IsResultsSuccess(json))
            return Fail(GetResultsDescription(json) ?? GetRootString(json, "error") ?? "PayPlus document creation failed.", json);

        return new PaymentTransactionResult
        {
            Success = true,
            ResponseCode = 0,
            DocumentNumber = GetRootString(json, "number"),
            DocumentUrl = GetRootString(json, "originalDocAddress"),
            RawJson = json,
        };
    }

    /// <summary>Re-parses a stored/callback PayPlus JSON payload - analogue of Cardcom's ParseLpResult,
    /// used to re-interpret webhook/callback bodies without a fresh API call.</summary>
    public ValidateCallbackResult ParsePayPlusResult(string json) => MapTransactionResult(json);

    /// <summary>Extract last4/brand from a stored PayPlus JSON payload - analogue of Cardcom's
    /// ExtractCardDisplayFields; reuses the same provider-neutral <see cref="CardcomCardDisplayFields"/> shape.</summary>
    public CardcomCardDisplayFields ExtractCardDisplayFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CardcomCardDisplayFields();

        var last4 = GetDataString(json, "four_digits");
        var brand = GetDataString(json, "brand_name") ?? GetDataString(json, "clearing_name");

        // Transactions/View nests these under data[0].data.card_information.
        if ((last4 == null || brand == null)
            && TryResolveTransactionNodes(json, out _, out var extra)
            && extra.ValueKind == JsonValueKind.Object
            && extra.TryGetProperty("card_information", out var card)
            && card.ValueKind == JsonValueKind.Object)
        {
            last4 ??= TryGetStringProperty(card, "four_digits", out var l4) ? l4 : null;
            brand ??= (TryGetStringProperty(card, "brand_name", out var b) ? b : null)
                ?? (TryGetStringProperty(card, "clearing_name", out var c) ? c : null);
        }

        return new CardcomCardDisplayFields { Last4Digits = last4, CardBrand = brand };
    }

    /// <summary>
    /// Extract the reusable saved-card token from an IPN/callback payload (token_uid + card display +
    /// expiry). Transactions/View does NOT return the token - only the checkout IPN/callback does.
    /// NOTE: the sandbox IPN was observed appending the card's last4 to token_uid; stored as-is because
    /// the vendor plugin stores and replays the same value.
    /// </summary>
    public PayPlusSavedTokenFields ExtractSavedTokenFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new PayPlusSavedTokenFields();

        var mm = GetDataString(json, "expiry_month");
        var yy = GetDataString(json, "expiry_year");
        var exp = !string.IsNullOrWhiteSpace(mm) && !string.IsNullOrWhiteSpace(yy)
            ? $"{mm.Trim().PadLeft(2, '0')}{(yy.Trim().Length > 2 ? yy.Trim()[^2..] : yy.Trim())}"
            : null;
        var display = ExtractCardDisplayFields(json);

        return new PayPlusSavedTokenFields
        {
            Token = GetDataString(json, "token_uid"),
            CardExpirationMMYY = exp,
            Last4Digits = display.Last4Digits,
            CardBrand = display.CardBrand,
        };
    }

    private static ValidateCallbackResult MapTransactionResult(string json)
    {
        var success = IsResultsSuccess(json);
        var transactionType = GetDataString(json, "transaction_type");
        return new ValidateCallbackResult
        {
            Success = success,
            IsPending = false,
            ResponseCode = GetResultsCode(json) ?? (success ? 0 : -1),
            Description = GetResultsDescription(json),
            TranzactionId = GetDataString(json, "transaction_uid"),
            Token = GetDataString(json, "token_uid"),
            Amount = GetDataDecimal(json, "amount"),
            Last4Digits = GetDataString(json, "four_digits"),
            CardBrand = GetDataString(json, "brand_name") ?? GetDataString(json, "clearing_name"),
            NumOfPayments = GetDataInt(json, "number_of_payments"),
            RawJson = json,
        };
    }

    private static PaymentTransactionResult MapTransactionalResult(string json)
    {
        var success = IsResultsSuccess(json);
        return new PaymentTransactionResult
        {
            Success = success,
            ResponseCode = GetResultsCode(json) ?? (success ? 0 : -1),
            Description = GetResultsDescription(json),
            TranzactionId = GetDataString(json, "transaction_uid"),
            ApprovalNumber = GetDataString(json, "approval_num"),
            RawJson = json,
        };
    }

    private static PaymentTransactionResult Fail(string description, string? raw = null) =>
        new() { Success = false, ResponseCode = -1, Description = description, RawJson = raw };

    private static CreateHostedSessionResult FailCreate(string code, string desc, string? raw = null) =>
        new() { Success = false, ErrorCode = code, ErrorDescription = desc, RawJson = raw };

    private async Task<string?> PostJsonAsync(
        SitePaymentCredentials credentials,
        string path,
        Dictionary<string, object?> body,
        CancellationToken cancelToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var baseUrl = credentials.TestMode ? SandboxBaseUrl : ProdBaseUrl;
            var url = baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };
            // PayPlus expects a single Authorization header carrying a JSON object (confirmed against the
            // official WooCommerce plugin and a live sandbox 403 on separate api-key/secret-key headers).
            // TryAddWithoutValidation: the value is not a standard "<scheme> <token>" Authorization format.
            var authJson = JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["api_key"] = credentials.ApiName,
                ["secret_key"] = credentials.ApiPassword,
            });
            httpRequest.Headers.TryAddWithoutValidation("Authorization", authJson);

            using var response = await client.SendAsync(httpRequest, cancelToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("PayPlus {Path} HTTP {Status}: {Body}", path, (int)response.StatusCode, text);
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayPlus request failed: {Path}", path);
            return null;
        }
    }

    // --- JSON helpers: PayPlus wraps most responses as { results: { status, code, description }, data: {...} }.
    // Some endpoints (Invoice+ docs) return fields at the root instead - handled by GetRootString/TryGetObjectProperty.

    private static bool IsResultsSuccess(string json)
    {
        if (!TryGetObjectProperty(json, "results", out var results))
            return false;
        if (results.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.String)
            return string.Equals(status.GetString(), "success", StringComparison.OrdinalIgnoreCase);
        if (results.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number)
            return code.GetInt32() == 0;
        return false;
    }

    private static int? GetResultsCode(string json)
    {
        if (!TryGetObjectProperty(json, "results", out var results))
            return null;
        return results.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number
            ? code.GetInt32()
            : null;
    }

    private static string? GetResultsDescription(string json)
    {
        if (!TryGetObjectProperty(json, "results", out var results))
            return null;
        return results.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;
    }

    private static string? GetDataString(string json, string name)
    {
        if (TryGetObjectProperty(json, "data", out var data) && TryGetStringProperty(data, name, out var fromData))
            return fromData;
        return TryGetStringProperty(RootOf(json), name, out var fromRoot) ? fromRoot : null;
    }

    /// <summary>
    /// Transactions/View wraps the transaction in a data ARRAY ({data:[{transaction:{...},data:{...}}]}),
    /// unlike IPN/charge responses where fields sit directly under a data OBJECT. Resolves both shapes:
    /// `transaction` = the node carrying transaction_type/amount/transaction_uid, `extra` = the sibling
    /// node carrying card_information/customer data (same node in the flat shape).
    /// </summary>
    private static bool TryResolveTransactionNodes(string json, out JsonElement transaction, out JsonElement extra)
    {
        transaction = default;
        extra = default;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return false;
            if (!doc.RootElement.TryGetProperty("data", out var data)) return false;

            if (data.ValueKind == JsonValueKind.Object)
            {
                transaction = data.Clone();
                extra = data.Clone();
                return true;
            }

            if (data.ValueKind == JsonValueKind.Array && data.GetArrayLength() > 0)
            {
                var first = data[0];
                if (first.ValueKind != JsonValueKind.Object) return false;
                transaction = first.TryGetProperty("transaction", out var tx) && tx.ValueKind == JsonValueKind.Object
                    ? tx.Clone() : first.Clone();
                extra = first.TryGetProperty("data", out var ex) && ex.ValueKind == JsonValueKind.Object
                    ? ex.Clone() : first.Clone();
                return true;
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Field lookup that understands both the flat (IPN/charge) and nested-array (Transactions/View) shapes.</summary>
    private static string? GetTransactionString(string json, string name)
    {
        if (TryResolveTransactionNodes(json, out var tx, out _) && TryGetStringProperty(tx, name, out var fromTx))
            return fromTx;
        return GetDataString(json, name);
    }

    private static decimal? GetTransactionDecimal(string json, string name)
    {
        var raw = GetTransactionString(json, name);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string? GetRootString(string json, string name) =>
        TryGetStringProperty(RootOf(json), name, out var value) ? value : null;

    private static decimal? GetDataDecimal(string json, string name)
    {
        var raw = GetDataString(json, name);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static int? GetDataInt(string json, string name)
    {
        var raw = GetDataString(json, name);
        return int.TryParse(raw, out var i) ? i : null;
    }

    private static JsonElement RootOf(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }

    private static bool TryGetObjectProperty(string json, string name, out JsonElement obj)
    {
        obj = default;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            if (!doc.RootElement.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
                return false;
            obj = el.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryGetStringProperty(JsonElement parent, string name, out string? value)
    {
        value = null;
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var p))
            return false;
        value = p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
        return value != null;
    }
}
