using System.Linq;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using George.Common.Payment;
using George.Services.Payments;
using Microsoft.Extensions.Logging;

namespace George.Services.Payments.Cardcom;

public sealed class CardcomGateway : IPaymentGatewayProvider
{
    public const string HttpClientName = "CardcomApi";
    private const string BaseUrl = "https://secure.cardcom.solutions/api/v11/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CardcomGateway> _logger;

    public CardcomGateway(IHttpClientFactory httpClientFactory, ILogger<CardcomGateway> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderId => PaymentGatewayProviderId.Cardcom;

    public PaymentGatewayCapabilities Capabilities { get; } = new()
    {
        SupportsHostedSession = true,
        SupportsTokenCharge = true,
        SupportsCaptureAuthorization = true,
        SupportsPartialRefund = true,
        SupportsVoidAuthorization = true,
        SupportsMotoPortal = true,
        SupportsCaptureAboveAuth = false,
    };

    public async Task<CreateHostedSessionResult> CreateHostedSessionAsync(
        SitePaymentCredentials credentials,
        CreateHostedSessionRequest request,
        CancellationToken cancelToken = default)
    {
        var terminal = credentials.TerminalNumber ?? 0;
        if (terminal <= 0 || string.IsNullOrWhiteSpace(credentials.ApiName))
            return FailCreate("config", "Cardcom terminal or API name is not configured.");

        // Picking flow: J5 hold + token (charge at picking via Do Transaction), not SuspendedDeal module.
        var operation = request.UseAuthorizationHold
            ? "CreateTokenOnly"
            : request.SaveCard ? "ChargeAndCreateToken" : "ChargeOnly";

        var language = string.IsNullOrWhiteSpace(request.Language) ? "he" : request.Language.Trim();

        var body = new Dictionary<string, object?>
        {
            ["TerminalNumber"] = terminal,
            ["ApiName"] = credentials.ApiName,
            ["Operation"] = operation,
            ["Amount"] = request.Amount,
            ["ReturnValue"] = request.ReturnValue,
            ["ProductName"] = Truncate(request.ProductName ?? $"Order {request.OrderId}", 50),
            ["Language"] = language,
            ["ISOCoinId"] = MapCurrency(credentials.Currency),
            ["SuccessRedirectUrl"] = request.SuccessRedirectUrl,
            ["FailedRedirectUrl"] = request.FailedRedirectUrl,
            ["WebHookUrl"] = request.WebHookUrl,
        };

        ApplyAdvancedDefinition(body, request);
        ApplyUiDefinition(body, credentials, request);
        MergeProviderExtrasJson(body, credentials.ProviderExtrasJson);

        var json = await PostJsonAsync("LowProfile/Create", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return FailCreate("http", "Empty response from Cardcom.");

        var responseCode = GetInt(json, "ResponseCode");
        if (responseCode != 0)
            return FailCreate(responseCode.ToString(), GetString(json, "Description") ?? "Create failed", json);

        return new CreateHostedSessionResult
        {
            Success = true,
            PaymentUrl = GetString(json, "Url") ?? GetString(json, "url"),
            LowProfileId = GetString(json, "LowProfileId") ?? GetString(json, "lowProfileId"),
            RawJson = json,
        };
    }

    /// <summary>Parse GetLpResult JSON (also used to re-read stored callback payloads).</summary>
    public ValidateCallbackResult ParseLpResult(string json) => MapLpResult(json);

    /// <summary>Extract last4 / brand / expiry from Cardcom callback JSON (MapLpResult + regex fallback).</summary>
    public CardcomCardDisplayFields ExtractCardDisplayFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CardcomCardDisplayFields();

        var parsed = MapLpResult(json);
        var last4 = parsed.Last4Digits;
        var brand = parsed.CardBrand;
        var tokenEx = parsed.TokenExDate;
        var cardExp = parsed.CardExpirationMMYY;

        if (string.IsNullOrWhiteSpace(last4))
        {
            last4 = FormatLast4(FindJsonStringValue(json, "Last4CardDigitsString"));
            if (string.IsNullOrWhiteSpace(last4))
                last4 = FormatLast4(ExtractJsonScalarValue(json, "Last4CardDigits"));
        }

        if (string.IsNullOrWhiteSpace(brand))
        {
            brand = FirstNonEmpty(
                FindJsonStringValue(json, "Brand"),
                FindJsonStringValue(json, "CardName"));
        }

        if (string.IsNullOrWhiteSpace(tokenEx))
            tokenEx = FindJsonStringValue(json, "TokenExDate");

        if (string.IsNullOrWhiteSpace(cardExp))
            cardExp = FindCardExpirationInJson(json);

        return new CardcomCardDisplayFields
        {
            Last4Digits = last4,
            CardBrand = brand,
            TokenExDate = tokenEx,
            CardExpirationMMYY = cardExp,
        };
    }

    private static string? ExtractJsonScalarValue(string json, string propertyName)
    {
        var quoted = FindJsonStringValue(json, propertyName);
        if (!string.IsNullOrWhiteSpace(quoted))
            return quoted;

        var match = Regex.Match(
            json,
            $@"""{Regex.Escape(propertyName)}""\s*:\s*(\d+)",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }

    public async Task<ValidateCallbackResult> ValidateCallbackAsync(
        SitePaymentCredentials credentials,
        ValidateCallbackRequest request,
        CancellationToken cancelToken = default)
    {
        var terminal = credentials.TerminalNumber ?? 0;
        var body = new Dictionary<string, object?>
        {
            ["TerminalNumber"] = terminal,
            ["ApiName"] = credentials.ApiName,
            ["LowProfileId"] = request.LowProfileId,
        };

        var json = await PostJsonAsync("LowProfile/GetLpResult", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return new ValidateCallbackResult { Success = false, ResponseCode = -1, Description = "Empty response" };

        try
        {
            return MapLpResult(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cardcom GetLpResult parse failed for LowProfileId {LowProfileId}", request.LowProfileId);
            return new ValidateCallbackResult
            {
                Success = false,
                ResponseCode = -1,
                Description = ex.Message,
                RawJson = json,
            };
        }
    }

    public async Task<PaymentTransactionResult> CaptureAuthorizationAsync(
        SitePaymentCredentials credentials,
        CaptureAuthorizationRequest request,
        CancellationToken cancelToken = default)
        => await DoTransactionAsync(credentials, request.Amount, request.Token, request.CardExpirationMMYY,
            request.ApprovalNumber, isRefund: false, mtiVoid: false, jValidateHold: false, request.ExternalUniqTranId,
            request.CreateDocument ? request.Document : null, request.CardOwner, cancelToken,
            numOfPayments: request.NumOfPayments)
            .ConfigureAwait(false);

    public async Task<PaymentTransactionResult> PlaceTokenAuthorizationHoldAsync(
        SitePaymentCredentials credentials,
        PlaceTokenAuthorizationHoldRequest request,
        CancellationToken cancelToken = default)
        => await DoTransactionAsync(credentials, request.Amount, request.Token, request.CardExpirationMMYY,
            approvalNumber: null, isRefund: false, mtiVoid: false, jValidateHold: true, request.ExternalUniqTranId,
            document: null, cardOwner: null, cancelToken)
            .ConfigureAwait(false);

    public async Task<PaymentTransactionResult> ChargeTokenAsync(
        SitePaymentCredentials credentials,
        ChargeTokenRequest request,
        CancellationToken cancelToken = default)
        => await DoTransactionAsync(credentials, request.Amount, request.Token, request.CardExpirationMMYY,
            request.ApprovalNumber, isRefund: false, mtiVoid: false, jValidateHold: false, request.ExternalUniqTranId,
            request.CreateDocument ? request.Document : null, request.CardOwner, cancelToken,
            numOfPayments: request.NumOfPayments)
            .ConfigureAwait(false);

    public async Task<PaymentTransactionResult> CreateDocumentAsync(
        SitePaymentCredentials credentials,
        CreateCardcomDocumentRequest request,
        CancellationToken cancelToken = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.ApiName))
            return new PaymentTransactionResult
            {
                Success = false,
                ResponseCode = -1,
                Description = "Cardcom API name is not configured.",
            };

        if (string.IsNullOrWhiteSpace(credentials.ApiPassword))
            return new PaymentTransactionResult
            {
                Success = false,
                ResponseCode = -1,
                Description = "Cardcom API password is required to issue invoices.",
            };

        var body = new Dictionary<string, object?>
        {
            ["ApiName"] = credentials.ApiName,
            ["ApiPassword"] = credentials.ApiPassword,
            ["Document"] = CardcomDocumentPayload.ToDictionary(request.Document),
        };

        // Documents reference charge transactions (DealNumbers), which live on the charge terminal when a
        // second (no-CVV) terminal is configured — send the same terminal so the deal lookup matches.
        var terminal = credentials.EffectiveChargeTerminalNumber ?? 0;
        if (terminal > 0)
            body["TerminalNumber"] = terminal;

        if (!string.IsNullOrWhiteSpace(request.TranzactionId))
        {
            body["DealNumbers"] = new[]
            {
                new Dictionary<string, object?> { ["DealNumber"] = request.TranzactionId.Trim() },
            };
        }

        var json = await PostJsonAsync("Documents/CreateDocument", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return new PaymentTransactionResult { Success = false, ResponseCode = -1, Description = "Empty response" };

        return ParseTransactionResult(json);
    }

    public async Task<PaymentTransactionResult> RefundAsync(
        SitePaymentCredentials credentials,
        RefundRequest request,
        CancellationToken cancelToken = default)
    {
        if (!string.IsNullOrWhiteSpace(request.OriginalTranzactionId)
            && !string.IsNullOrWhiteSpace(credentials.ApiPassword)
            && TryParseTransactionId(request.OriginalTranzactionId, out var txId))
        {
            var byTxId = await RefundByTransactionIdAsync(credentials, txId, request.Amount, cancelToken)
                .ConfigureAwait(false);
            if (byTxId.Success)
                return byTxId;

            if (!string.IsNullOrWhiteSpace(request.Token)
                && !string.IsNullOrWhiteSpace(request.CardExpirationMMYY))
            {
                _logger.LogWarning(
                    "RefundByTransactionId failed ({Code}: {Description}); retrying via token refund.",
                    byTxId.ResponseCode, byTxId.Description);
                var byToken = await DoTransactionAsync(credentials, request.Amount, request.Token,
                    request.CardExpirationMMYY, approvalNumber: null, isRefund: true, mtiVoid: false,
                    jValidateHold: false, request.ExternalUniqTranId, document: null, cardOwner: null, cancelToken)
                    .ConfigureAwait(false);
                if (byToken.Success)
                    return byToken;

                return PreferRefundError(byTxId, byToken);
            }

            return byTxId;
        }

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.CardExpirationMMYY))
        {
            return new PaymentTransactionResult
            {
                Success = false,
                ResponseCode = -1,
                Description = string.IsNullOrWhiteSpace(credentials.ApiPassword)
                    ? "Cardcom API password is required for refunds (or provide a valid capture transaction id)."
                    : "Missing capture transaction id and card token for refund.",
            };
        }

        return await DoTransactionAsync(credentials, request.Amount, request.Token, request.CardExpirationMMYY,
            approvalNumber: null, isRefund: true, mtiVoid: false, jValidateHold: false, request.ExternalUniqTranId,
            document: null, cardOwner: null, cancelToken)
            .ConfigureAwait(false);
    }

    private static PaymentTransactionResult PreferRefundError(
        PaymentTransactionResult primary,
        PaymentTransactionResult fallback)
    {
        if (IsCardcomRefundPermissionError(primary.Description))
            return primary;
        if (IsCardcomRefundPermissionError(fallback.Description))
            return fallback;
        return !string.IsNullOrWhiteSpace(fallback.Description) ? fallback : primary;
    }

    internal static bool IsCardcomRefundPermissionError(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;
        return description.Contains("הרשאת API", StringComparison.OrdinalIgnoreCase)
            || description.Contains("ביטול עסקה", StringComparison.OrdinalIgnoreCase)
            || description.Contains("API permission", StringComparison.OrdinalIgnoreCase);
    }

    internal static string EnhanceCardcomRefundErrorMessage(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return "Refund failed.";
        if (!IsCardcomRefundPermissionError(description))
            return description;
        return description.Trim()
            + " פנו ל-Cardcom להפעלת הרשאת זיכוי/ביטול עבור משתמש ה-API (ApiName) שמוגדר בהגדרות Cardcom.";
    }

    private async Task<PaymentTransactionResult> RefundByTransactionIdAsync(
        SitePaymentCredentials credentials,
        long transactionId,
        decimal amount,
        CancellationToken cancelToken)
    {
        var body = new Dictionary<string, object?>
        {
            ["ApiName"] = credentials.ApiName,
            ["ApiPassword"] = credentials.ApiPassword,
            ["TransactionId"] = transactionId,
            ["PartialSum"] = amount,
            ["CancelOnly"] = false,
            ["AllowMultipleRefunds"] = true,
        };

        var json = await PostJsonAsync("Transactions/RefundByTransactionId", body, cancelToken).ConfigureAwait(false);
        if (json == null)
            return new PaymentTransactionResult { Success = false, ResponseCode = -1, Description = "Empty response" };

        var responseCode = GetInt(json, "ResponseCode");
        var success = responseCode == 0;
        return new PaymentTransactionResult
        {
            Success = success,
            ResponseCode = responseCode,
            Description = GetString(json, "Description"),
            TranzactionId = GetString(json, "NewTranzactionId") ?? GetString(json, "newTranzactionId"),
            RawJson = json,
        };
    }

    private static bool TryParseTransactionId(string value, out long transactionId)
    {
        transactionId = 0;
        var trimmed = value.Trim();
        return long.TryParse(trimmed, out transactionId) && transactionId > 0;
    }

    public async Task<PaymentTransactionResult> VoidAuthorizationAsync(
        SitePaymentCredentials credentials,
        VoidAuthorizationRequest request,
        CancellationToken cancelToken = default)
        => await DoTransactionAsync(credentials, request.Amount, request.Token, request.CardExpirationMMYY,
            request.ApprovalNumber, isRefund: false, mtiVoid: true, jValidateHold: false, request.ExternalUniqTranId,
            document: null, cardOwner: null, cancelToken)
            .ConfigureAwait(false);

    /// <summary>
    /// Cardcom <c>Transactions/GetTransactionInfoById</c> — lookup by internal deal / transaction number.
    /// </summary>
    public async Task<CardcomTransactionInfoResult> GetTransactionInfoByIdAsync(
        SitePaymentCredentials credentials,
        long internalDealNumber,
        CancellationToken cancelToken = default)
    {
        if (credentials.TerminalNumber is not > 0 || string.IsNullOrWhiteSpace(credentials.ApiName))
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = -1,
                Description = "Terminal and API name are required.",
            };
        }

        if (string.IsNullOrWhiteSpace(credentials.ApiPassword))
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = -1,
                Description = "Cardcom API password is required.",
            };
        }

        // Inquiry is used for WEBSITE (plugin) transactions, which live on the primary terminal.
        var body = new Dictionary<string, object?>
        {
            ["TerminalNumber"] = credentials.TerminalNumber.Value,
            ["UserName"] = credentials.ApiName,
            ["UserPassword"] = credentials.ApiPassword,
            ["InternalDealNumber"] = internalDealNumber,
        };

        var json = await PostJsonAsync("Transactions/GetTransactionInfoById", body, cancelToken).ConfigureAwait(false);
        if (json == null)
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = -1,
                Description = "Empty response from Cardcom.",
            };
        }

        var result = ParseTransactionInfoResult(json);

        // Charge transactions (J4 capture / token charge) live on the no-CVV charge terminal when one
        // is configured, while holds and the payment page stay on the primary — retry there before
        // giving up, so verification can find charges made on the second terminal.
        var chargeTerminal = credentials.EffectiveChargeTerminalNumber;
        if (!result.Success && chargeTerminal is > 0 && chargeTerminal != credentials.TerminalNumber)
        {
            body["TerminalNumber"] = chargeTerminal.Value;
            var retryJson = await PostJsonAsync("Transactions/GetTransactionInfoById", body, cancelToken)
                .ConfigureAwait(false);
            if (retryJson != null)
            {
                var retry = ParseTransactionInfoResult(retryJson);
                if (retry.Success)
                    return retry;
            }
        }

        return result;
    }

    public static CardcomTransactionInfoResult ParseTransactionInfoResult(string json)
    {
        // Cardcom occasionally returns a non-object body (e.g. "\"\"" — a bare JSON string) for
        // GetTransactionInfoById. Every property reader then falls back to its default, which made
        // ResponseCode=0 + empty DealType read as a successful final charge (order 6321, 2026-08-20:
        // an uncharged order was marked Paid from exactly such a response). Anything that is not a
        // JSON object carrying ResponseCode is an inquiry failure, never a charge.
        if (!TryGetRequiredResponseCode(json, out var responseCode))
        {
            return new CardcomTransactionInfoResult
            {
                Success = false,
                ResponseCode = -1,
                Description = "Unexpected response from Cardcom transaction inquiry.",
                RawJson = json,
            };
        }
        var dealType = GetString(json, "DealType");
        var isRefund = TryGetBool(json, "IsRefund");
        var isHold = IsCardcomTransactionInfoAuthorizationHold(responseCode, dealType, json);
        var isFinalCharge = IsCardcomTransactionInfoFinalCharge(responseCode, dealType, isRefund, isHold);
        var amount = TryGetDecimal(json, "Amount");
        var txId = GetString(json, "TranzactionId") ?? GetString(json, "TransactionId");

        // Invoice document created with the transaction (same top-level/DocumentInfo shape as ParseTransactionResult).
        var docNum = NormalizeDocumentNumber(GetString(json, "DocumentNumber"));
        var docUrl = GetString(json, "DocumentUrl");
        if (string.IsNullOrWhiteSpace(docNum) || string.IsNullOrWhiteSpace(docUrl))
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Object
                    && TryGetObjectProperty(root, "DocumentInfo", out var di))
                {
                    docNum ??= NormalizeDocumentNumber(GetStringProperty(di, "DocumentNumber"));
                    docUrl ??= GetStringProperty(di, "DocumentUrl");
                }
            }
            catch (JsonException)
            {
                // document info is best-effort; the inquiry result is still useful without it
            }
        }

        return new CardcomTransactionInfoResult
        {
            Success = responseCode == 0 || isFinalCharge || isHold,
            ResponseCode = responseCode,
            Description = GetString(json, "Description"),
            TranzactionId = txId,
            Amount = amount,
            DealType = dealType,
            IsRefund = isRefund,
            RawJson = json,
            IsFinalCharge = isFinalCharge,
            IsAuthorizationHold = isHold,
            DocumentNumber = docNum,
            DocumentUrl = docUrl,
        };
    }

    /// <summary>J5 / validate holds use response codes 700/701 or deal types that are not final debits.</summary>
    internal static bool IsCardcomTransactionInfoAuthorizationHold(int responseCode, string? dealType, string json)
    {
        if (responseCode is 700 or 701)
            return true;

        var j = TryGetInt(json, "JParameter");
        if (j is < 0)
            j = TryGetInt(json, "J");
        if (j == 5)
            return true;

        var dt = (dealType ?? "").Trim();
        if (dt.Equals("Information", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    internal static bool IsCardcomTransactionInfoFinalCharge(
        int responseCode,
        string? dealType,
        bool? isRefund,
        bool isAuthorizationHold)
    {
        if (isRefund == true || isAuthorizationHold)
            return false;

        if (responseCode != 0)
            return false;

        var dt = (dealType ?? "").Trim();
        if (dt.Length == 0)
            return true;

        if (dt.Equals("Debit", StringComparison.OrdinalIgnoreCase)
            || dt.Equals("ForcedCharge", StringComparison.OrdinalIgnoreCase)
            || dt.Equals("CashTransaction", StringComparison.OrdinalIgnoreCase))
            return true;

        if (dt.Equals("Refund", StringComparison.OrdinalIgnoreCase)
            || dt.Equals("Cancel", StringComparison.OrdinalIgnoreCase)
            || dt.Equals("Discharge", StringComparison.OrdinalIgnoreCase))
            return false;

        return false;
    }

    private static bool? TryGetBool(string json, string name)
    {
        // These scalar readers must use SafeTryGetProperty — TryGetObjectProperty only matches
        // object-valued properties, so Amount/IsRefund/JParameter always read as missing.
        using var doc = JsonDocument.Parse(json);
        if (!SafeTryGetProperty(doc.RootElement, name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(el.GetString(), out var b) => b,
            _ => null,
        };
    }

    private static decimal? TryGetDecimal(string json, string name)
    {
        using var doc = JsonDocument.Parse(json);
        if (!SafeTryGetProperty(doc.RootElement, name, out var el))
            return null;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetDecimal(out var d))
            return d;
        if (el.ValueKind == JsonValueKind.String
            && decimal.TryParse(el.GetString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
            return parsed;
        return null;
    }

    /// <summary>False when the body is not parseable JSON, not an object, or lacks ResponseCode.</summary>
    private static bool TryGetRequiredResponseCode(string json, out int responseCode)
    {
        responseCode = -1;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!SafeTryGetProperty(doc.RootElement, "ResponseCode", out var el))
                return false;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
            {
                responseCode = i;
                return true;
            }
            if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed))
            {
                responseCode = parsed;
                return true;
            }
            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int TryGetInt(string json, string name)
    {
        using var doc = JsonDocument.Parse(json);
        if (!SafeTryGetProperty(doc.RootElement, name, out var el))
            return -1;
        if (el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i))
            return i;
        if (el.ValueKind == JsonValueKind.String && int.TryParse(el.GetString(), out var parsed))
            return parsed;
        return -1;
    }

    public async Task<TestConnectionResult> TestConnectionAsync(
        SitePaymentCredentials credentials,
        CancellationToken cancelToken = default)
    {
        if (credentials.TerminalNumber is not > 0 || string.IsNullOrWhiteSpace(credentials.ApiName))
            return new TestConnectionResult { Success = false, Message = "Terminal and API name are required." };

        // Minimal validation: GetLpResult with dummy id returns structured error if credentials work.
        var body = new Dictionary<string, object?>
        {
            ["TerminalNumber"] = credentials.TerminalNumber,
            ["ApiName"] = credentials.ApiName,
            ["LowProfileId"] = "00000000-0000-0000-0000-000000000000",
        };
        var json = await PostJsonAsync("LowProfile/GetLpResult", body, cancelToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
            return new TestConnectionResult { Success = false, Message = "No response from Cardcom." };

        var code = GetInt(json, "ResponseCode");
        // Non-zero is expected for invalid LowProfileId; auth errors often use specific codes.
        if (json.Contains("ApiName", StringComparison.OrdinalIgnoreCase) && json.Contains("Terminal", StringComparison.OrdinalIgnoreCase))
            return new TestConnectionResult { Success = true, Message = $"Cardcom reachable (response {code})." };

        return new TestConnectionResult
        {
            Success = code == 0 || code > 0,
            Message = GetString(json, "Description") ?? $"Response code {code}",
        };
    }

    private async Task<PaymentTransactionResult> DoTransactionAsync(
        SitePaymentCredentials credentials,
        decimal amount,
        string? token,
        string? cardExpiration,
        string? approvalNumber,
        bool isRefund,
        bool mtiVoid,
        bool jValidateHold,
        string externalUniqTranId,
        CardcomTransactionDocument? document,
        CardcomCardOwnerContact? cardOwner,
        CancellationToken cancelToken,
        int numOfPayments = 1)
    {
        // Terminal routing with a second (no-CVV) charge terminal configured:
        // - ONLY the actual charge (J4 capture / direct token charge) and its token-refund go to the charge
        //   terminal — these are the operations a CVV-requiring terminal rejects for token transactions.
        // - Authorization holds (J5) stay on the PRIMARY terminal (token creation + hold work there today),
        //   and voiding a hold must hit the terminal that placed it — also the primary.
        // The hosted payment page (card entry, with CVV) stays on the primary too — see CreateHostedSessionAsync.
        var useChargeTerminal = !jValidateHold && !mtiVoid;
        var terminal = (useChargeTerminal ? credentials.EffectiveChargeTerminalNumber : credentials.TerminalNumber) ?? 0;
        // Installments apply ONLY to the actual charge; holds, voids and refunds are always single-payment.
        var effectivePayments = (!isRefund && !mtiVoid && !jValidateHold) ? Math.Clamp(numOfPayments, 1, 36) : 1;
        var body = new Dictionary<string, object?>
        {
            ["TerminalNumber"] = terminal,
            ["ApiName"] = credentials.ApiName,
            ["Amount"] = amount,
            ["ISOCoinId"] = MapCurrency(credentials.Currency),
            ["ExternalUniqTranId"] = externalUniqTranId,
            ["ExternalUniqTranIdResponse"] = false,
            ["NumOfPayments"] = effectivePayments,
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            body["Token"] = token;
            body["CardExpirationMMYY"] = cardExpiration;
        }

        ApplyCardOwnerContact(body, cardOwner);

        var advanced = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(credentials.ApiPassword))
            advanced["ApiPassword"] = credentials.ApiPassword;
        if (isRefund)
            advanced["IsRefund"] = true;
        if (mtiVoid)
        {
            advanced["MTI"] = 420;
            if (!string.IsNullOrWhiteSpace(approvalNumber))
                advanced["ApprovalNumber"] = approvalNumber;
        }
        else if (jValidateHold)
        {
            advanced["JValidateType"] = 5;
        }
        else if (!string.IsNullOrWhiteSpace(approvalNumber))
        {
            advanced["ApprovalNumber"] = approvalNumber;
        }

        if (advanced.Count > 0)
            body["Advanced"] = advanced;

        if (document != null)
            body["Document"] = CardcomDocumentPayload.ToDictionary(document);

        _logger.LogInformation(
            "Cardcom Transactions/Transaction: externalId={ExternalUniqTranId}, amount={Amount}, terminal={Terminal}, " +
            "tokenShape={TokenShape}, tokenMask={TokenMask}, cardExp={CardExp}, hasApproval={HasApproval}, " +
            "isRefund={IsRefund}, mtiVoid={MtiVoid}, jValidateHold={JValidateHold}, hasDocument={HasDocument}, hasCardOwner={HasCardOwner}, numOfPayments={NumOfPayments}",
            externalUniqTranId,
            amount,
            terminal,
            DescribeTokenShape(token),
            MaskTokenForLog(token),
            MaskCardExpirationForLog(cardExpiration),
            !string.IsNullOrWhiteSpace(approvalNumber),
            isRefund,
            mtiVoid,
            jValidateHold,
            document != null,
            cardOwner != null,
            effectivePayments);

        var json = await PostJsonAsync("Transactions/Transaction", body, cancelToken).ConfigureAwait(false);
        if (json == null)
        {
            _logger.LogWarning(
                "Cardcom Transactions/Transaction empty response: externalId={ExternalUniqTranId}",
                externalUniqTranId);
            return new PaymentTransactionResult { Success = false, ResponseCode = -1, Description = "Empty response" };
        }

        var result = ParseTransactionResult(json);
        _logger.LogInformation(
            "Cardcom Transactions/Transaction response: externalId={ExternalUniqTranId}, success={Success}, " +
            "responseCode={ResponseCode}, description={Description}, txId={TxId}",
            externalUniqTranId,
            result.Success,
            result.ResponseCode,
            TruncateForLog(result.Description, 300),
            result.TranzactionId);
        return result;
    }

    private static string? MaskTokenForLog(string? token) =>
        string.IsNullOrWhiteSpace(token) || token.Length < 4 ? null : $"****{token.Trim()[^4..]}";

    private static string? MaskCardExpirationForLog(string? cardExpiration) =>
        string.IsNullOrWhiteSpace(cardExpiration) ? null : "****";

    private static string? TruncateForLog(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;
        var t = value.Trim();
        return t.Length <= max ? t : t[..max] + "...";
    }

    /// <summary>
    /// Cardcom <c>TransactionInfo.ResponseCode</c>: 0 = success; 700/701 = success for J2/J5 (authorization hold).
    /// See https://secure.cardcom.solutions/Api/v11/Docs (Transactions/Transaction response).
    /// </summary>
    public static bool IsCardcomTransactionResponseSuccess(int responseCode) =>
        responseCode is 0 or 700 or 701;

    private static PaymentTransactionResult ParseTransactionResult(string json)
    {
        var responseCode = GetInt(json, "ResponseCode");
        var success = IsCardcomTransactionResponseSuccess(responseCode);
        var docNum = NormalizeDocumentNumber(GetString(json, "DocumentNumber"));
        var docUrl = GetString(json, "DocumentUrl");

        if (string.IsNullOrWhiteSpace(docNum) || string.IsNullOrWhiteSpace(docUrl))
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && TryGetObjectProperty(root, "DocumentInfo", out var di))
            {
                docNum ??= NormalizeDocumentNumber(GetStringProperty(di, "DocumentNumber"));
                docUrl ??= GetStringProperty(di, "DocumentUrl");
            }
        }

        return new PaymentTransactionResult
        {
            Success = success,
            ResponseCode = responseCode,
            Description = GetString(json, "Description"),
            TranzactionId = GetString(json, "TranzactionId") ?? GetString(json, "tranzactionId"),
            ApprovalNumber = GetString(json, "ApprovalNumber"),
            DocumentNumber = docNum,
            DocumentUrl = docUrl,
            RawJson = json,
        };
    }

    private static string? NormalizeDocumentNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!int.TryParse(value.Trim(), out var n))
            return value.Trim();
        return n > 0 ? value.Trim() : null;
    }

    private async Task<string?> PostJsonAsync(string path, Dictionary<string, object?> body, CancellationToken cancelToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            var url = BaseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
            using var response = await client.PostAsJsonAsync(url, body, JsonOptions, cancelToken).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cardcom {Path} HTTP {Status}: {Body}", path, (int)response.StatusCode, text);
            }
            return text;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cardcom request failed: {Path}", path);
            return null;
        }
    }

    private static ValidateCallbackResult MapLpResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return new ValidateCallbackResult
                {
                    Success = false,
                    ResponseCode = -1,
                    Description = "GetLpResult response is not a JSON object.",
                    RawJson = json,
                };
            }

            var responseCode = GetInt32Property(root, "ResponseCode");
            var description = GetStringProperty(root, "Description");
            var success = IsCardcomTransactionResponseSuccess(responseCode);
            var isPending = !success && IsPendingLpResult(responseCode, description);

            string? token = null, tokenEx = null, cardMonth = null, cardYear = null, approval = null, last4 = null;
            string? cardExpFromTokenInfo = null;

            if (TryGetObjectProperty(root, "TokenInfo", out var tokenInfo))
            {
                token = FirstNonEmpty(
                    GetStringProperty(tokenInfo, "Token"),
                    GetStringProperty(tokenInfo, "CardcomToken"));
                tokenEx = GetStringProperty(tokenInfo, "TokenExDate");
                cardMonth = GetCardMonthProperty(tokenInfo);
                cardYear = GetCardYearProperty(tokenInfo);
                cardExpFromTokenInfo = GetStringProperty(tokenInfo, "CardExpirationMMYY");
                approval = FirstNonEmpty(GetStringProperty(tokenInfo, "TokenApprovalNumber"))?.Trim();
                last4 = FirstNonEmpty(
                    GetStringProperty(tokenInfo, "Last4CardDigitsString"),
                    FormatLast4(GetStringProperty(tokenInfo, "Last4CardDigits")));
            }

            if (TryGetObjectProperty(root, "UIValues", out var uiValues))
            {
                cardMonth = FirstNonEmpty(cardMonth, GetCardMonthProperty(uiValues));
                cardYear = FirstNonEmpty(cardYear, GetCardYearProperty(uiValues));
            }

            token = FirstNonEmpty(token, GetStringProperty(root, "Token"));

            string? brand = null;
            string? tiApproval = null;
            int? numOfPayments = null;
            if (TryGetObjectProperty(root, "TranzactionInfo", out var ti))
            {
                var payments = GetInt32Property(ti, "NumberOfPayments");
                if (payments <= 0)
                    payments = GetInt32Property(ti, "NumOfPayments");
                if (payments > 0)
                    numOfPayments = payments;
                token = FirstNonEmpty(token, GetStringProperty(ti, "Token"));
                tiApproval = FirstNonEmpty(
                    GetStringProperty(ti, "ApprovalNumber"),
                    GetStringProperty(ti, "AuthNum"),
                    GetStringProperty(ti, "TokenApprovalNumber"));
                approval = FirstNonEmpty(approval, tiApproval)?.Trim();
                cardMonth = FirstNonEmpty(cardMonth, GetCardMonthProperty(ti));
                cardYear = FirstNonEmpty(cardYear, GetCardYearProperty(ti));
                cardExpFromTokenInfo = FirstNonEmpty(
                    cardExpFromTokenInfo,
                    GetStringProperty(ti, "CardExpirationMMYY"));
                last4 = FirstNonEmpty(
                    last4,
                    GetStringProperty(ti, "Last4CardDigitsString"),
                    FormatLast4(GetStringProperty(ti, "Last4CardDigits")));
                brand = FirstNonEmpty(
                    GetStringProperty(ti, "Brand"),
                    GetStringProperty(ti, "CardName"));
            }

            last4 = FirstNonEmpty(
                last4,
                GetStringProperty(root, "Last4CardDigitsString"),
                FormatLast4(GetStringProperty(root, "Last4CardDigits")));

            var cardExp = ResolveCardExpirationMMYY(cardExpFromTokenInfo, cardMonth, cardYear, tokenEx);

            token = FirstNonEmpty(token, FindCardcomTokenInJson(json));
            cardExp = FirstNonEmpty(cardExp, FindCardExpirationInJson(json));

            decimal? amount = GetDecimalProperty(root, "Amount");
            if (TryGetObjectProperty(root, "TranzactionInfo", out var tiAmount))
                amount = GetDecimalProperty(tiAmount, "Amount") ?? amount;
            if (TryGetObjectProperty(root, "SuspendedInfo", out var si))
                amount = GetDecimalProperty(si, "Amount") ?? amount;

            string? docNum = null, docUrl = null;
            if (TryGetObjectProperty(root, "DocumentInfo", out var di))
            {
                docNum = GetStringProperty(di, "DocumentNumber");
                docUrl = GetStringProperty(di, "DocumentUrl");
            }

            var suspendedDealId = GetStringProperty(root, "SuspendedDealId");
            if (string.IsNullOrWhiteSpace(suspendedDealId) && TryGetObjectProperty(root, "SuspendedInfo", out var suspendedInfo))
                suspendedDealId = GetStringProperty(suspendedInfo, "SuspendedDealId");

            return new ValidateCallbackResult
            {
                Success = success,
                IsPending = isPending,
                ResponseCode = responseCode,
                Description = description,
                ReturnValue = GetStringProperty(root, "ReturnValue"),
                Operation = GetStringProperty(root, "Operation"),
                TranzactionId = GetStringProperty(root, "TranzactionId"),
                SuspendedDealId = suspendedDealId,
                ApprovalNumber = FirstNonEmpty(
                    approval,
                    GetStringProperty(root, "ApprovalNumber"),
                    tiApproval),
                Token = token,
                TokenExDate = tokenEx,
                CardExpirationMMYY = cardExp,
                Last4Digits = last4,
                CardBrand = FirstNonEmpty(
                    brand,
                    GetStringProperty(root, "Brand"),
                    GetStringProperty(root, "CardName")),
                DocumentNumber = NormalizeDocumentNumber(docNum),
                DocumentUrl = docUrl,
                Amount = amount,
                NumOfPayments = numOfPayments,
                RawJson = json,
            };
        }
        catch (JsonException ex)
        {
            return new ValidateCallbackResult
            {
                Success = false,
                ResponseCode = -1,
                Description = ex.Message,
                RawJson = json,
            };
        }
        catch (InvalidOperationException ex)
        {
            return new ValidateCallbackResult
            {
                Success = false,
                ResponseCode = -1,
                Description = ex.Message,
                RawJson = json,
            };
        }
        catch (Exception ex)
        {
            return new ValidateCallbackResult
            {
                Success = false,
                ResponseCode = -1,
                Description = ex.Message,
                RawJson = json,
            };
        }
    }

    private static int MapCurrency(string currency) =>
        currency.Trim().ToUpperInvariant() switch
        {
            "USD" => 2,
            _ => 1,
        };

    private static void ApplyCardOwnerContact(Dictionary<string, object?> body, CardcomCardOwnerContact? cardOwner)
    {
        if (cardOwner == null)
            return;

        if (!string.IsNullOrWhiteSpace(cardOwner.Name))
            body["CardOwnerName"] = cardOwner.Name.Trim();
        if (!string.IsNullOrWhiteSpace(cardOwner.Phone))
            body["CardOwnerPhone"] = cardOwner.Phone.Trim();
        if (!string.IsNullOrWhiteSpace(cardOwner.Email))
            body["CardOwnerEmail"] = cardOwner.Email.Trim();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max];

    private static void ApplyAdvancedDefinition(Dictionary<string, object?> body, CreateHostedSessionRequest request)
    {
        var adv = GetOrCreateNestedDict(body, "AdvancedDefinition");

        if (request.UseAuthorizationHold)
            adv["JValidateType"] = 5;

        // Immediate charge: the customer's selection settles on the page itself.
        // J5 hold: the selection is read back from the callback (TranzactionInfo.NumOfPayments), stored on
        // the order, and honored by the post-picking token charge — see FinalizePickingPayment.
        adv["MinNumOfPayments"] = 1;
        adv["MaxNumOfPayments"] = Math.Clamp(request.MaxInstallments, 1, 36);
        adv["SelectedNumOfPayments"] = 1;

        if (request.UseVirtualTerminal)
        {
            adv["VirtualTerminal"] = new Dictionary<string, object?>
            {
                ["IsEnable"] = true,
                ["IsOpenSum"] = false,
            };
        }
    }

    private static void ApplyUiDefinition(
        Dictionary<string, object?> body,
        SitePaymentCredentials credentials,
        CreateHostedSessionRequest request)
    {
        var ui = GetOrCreateNestedDict(body, "UIDefinition");

        // Only when explicitly configured in site settings (no automatic CSS injection).
        if (!string.IsNullOrWhiteSpace(credentials.CssUrl))
            ui["CSSUrl"] = credentials.CssUrl.Trim();

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            ui["CardOwnerNameValue"] = request.CustomerName;
            ui["IsHideCardOwnerName"] = false;
        }
        else
        {
            ui["IsHideCardOwnerName"] = true;
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerPhone))
        {
            ui["CardOwnerPhoneValue"] = request.CustomerPhone;
            ui["IsHideCardOwnerPhone"] = false;
        }
        else
        {
            ui["IsHideCardOwnerPhone"] = true;
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            ui["CardOwnerEmailValue"] = request.CustomerEmail;
            ui["IsHideCardOwnerEmail"] = false;
        }
        else
        {
            ui["IsHideCardOwnerEmail"] = true;
        }

        ui["IsHideCardOwnerIdentityNumber"] = true;
        ui["IsCardOwnerEmailRequired"] = false;
    }

    /// <summary>
    /// Merges site-level JSON into Create body (e.g. UIDefinition / AdvancedDefinition overrides from Cardcom).
    /// Bit / Google Pay / PayPal visibility is controlled by the terminal in Cardcom — not in Swagger v11 fields.
    /// </summary>
    private static void MergeProviderExtrasJson(Dictionary<string, object?> body, string? extrasJson)
    {
        if (string.IsNullOrWhiteSpace(extrasJson))
            return;

        try
        {
            using var doc = JsonDocument.Parse(extrasJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return;

            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                var key = prop.Name;
                if (key is "TerminalNumber" or "ApiName" or "Amount" or "Operation")
                    continue;

                if (prop.Value.ValueKind == JsonValueKind.Object
                    && body.TryGetValue(key, out var existing)
                    && existing is Dictionary<string, object?> existingDict)
                {
                    MergeJsonObjectIntoDict(existingDict, prop.Value);
                    continue;
                }

                body[key] = JsonElementToObject(prop.Value);
            }
        }
        catch (JsonException)
        {
            // Invalid extras JSON must not break payment session creation.
        }
    }

    private static Dictionary<string, object?> GetOrCreateNestedDict(
        Dictionary<string, object?> body,
        string key)
    {
        if (body.TryGetValue(key, out var existing) && existing is Dictionary<string, object?> dict)
            return dict;

        var created = new Dictionary<string, object?>();
        body[key] = created;
        return created;
    }

    private static void MergeJsonObjectIntoDict(Dictionary<string, object?> target, JsonElement source)
    {
        foreach (var prop in source.EnumerateObject())
            target[prop.Name] = JsonElementToObject(prop.Value);
    }

    private static object? JsonElementToObject(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.Object => el.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
            JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    private static CreateHostedSessionResult FailCreate(string code, string desc, string? raw = null) =>
        new() { Success = false, ErrorCode = code, ErrorDescription = desc, RawJson = raw };

    private static int GetInt(JsonElement el, string name) =>
        GetInt32Property(el, name, 0);

    private static int GetInt(string json, string name)
    {
        using var doc = JsonDocument.Parse(json);
        return GetInt(doc.RootElement, name);
    }

    /// <summary>Non-zero GetLpResult while the customer has not finished payment — not a decline.</summary>
    private static bool IsPendingLpResult(int responseCode, string? description)
    {
        if (responseCode == 0) return false;
        if (responseCode is 700 or 701) return false;
        var d = (description ?? "").Trim();
        if (d.Length == 0) return true;
        if (d.Contains("ממתינ", StringComparison.OrdinalIgnoreCase)) return true;
        if (d.Contains("לא הושלם", StringComparison.OrdinalIgnoreCase)) return true;
        if (d.Contains("not completed", StringComparison.OrdinalIgnoreCase)) return true;
        if (d.Contains("pending", StringComparison.OrdinalIgnoreCase)) return true;
        // LPC form / issuer decline codes (e.g. 60000042) — real failure
        if (responseCode >= 60000000) return false;
        return true;
    }

    private static bool SafeTryGetProperty(JsonElement parent, string name, out JsonElement value)
    {
        value = default;
        if (parent.ValueKind != JsonValueKind.Object)
            return false;
        return parent.TryGetProperty(name, out value);
    }

    private static bool TryGetObjectProperty(JsonElement parent, string name, out JsonElement obj)
    {
        // BUG HISTORY (12/08/2026): this used to return true WITHOUT assigning `obj`, so every
        // nested-object read (TranzactionInfo/TokenInfo/DocumentInfo/SuspendedInfo) silently got
        // an empty element — NumberOfPayments/ApprovalNumber/etc. were never parsed from LP results.
        obj = default;
        if (!SafeTryGetProperty(parent, name, out var el) || el.ValueKind != JsonValueKind.Object)
            return false;
        obj = el;
        return true;
    }

    private static int GetInt32Property(JsonElement parent, string name, int defaultValue = -1)
    {
        if (!SafeTryGetProperty(parent, name, out var p))
            return defaultValue;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var i))
            return i;
        if (p.ValueKind == JsonValueKind.String && int.TryParse(p.GetString(), out i))
            return i;
        return defaultValue;
    }

    private static decimal? GetDecimalProperty(JsonElement parent, string name)
    {
        if (!SafeTryGetProperty(parent, name, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDecimal(out var d))
            return d;
        if (p.ValueKind == JsonValueKind.String && decimal.TryParse(p.GetString(), out d))
            return d;
        return null;
    }

    private static string? GetStringProperty(JsonElement parent, string name)
    {
        if (!SafeTryGetProperty(parent, name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    private static string? GetString(JsonElement el, string name) =>
        GetStringProperty(el, name);

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v))
                return v.Trim();
        }
        return null;
    }

    private static string? ResolveCardExpirationMMYY(
        string? direct,
        string? cardMonth,
        string? cardYear,
        string? tokenExDate)
    {
        // CardMonth/CardYear from TranzactionInfo are more reliable than TokenInfo.CardExpirationMMYY.
        if (!string.IsNullOrWhiteSpace(cardMonth) && !string.IsNullOrWhiteSpace(cardYear))
        {
            var yy = cardYear.Length >= 2 ? cardYear[^2..] : cardYear;
            return $"{cardMonth.PadLeft(2, '0')}{yy}";
        }

        var normalized = NormalizeCardExpirationMMYY(direct);
        if (!string.IsNullOrWhiteSpace(normalized))
            return normalized;

        return NormalizeCardExpirationFromTokenExDate(tokenExDate);
    }

    private static string? NormalizeCardExpirationMMYY(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            4 => digits,
            6 => digits[^4..],
            >= 8 => digits[^4..],
            _ => null,
        };
    }

    private static string? NormalizeCardExpirationFromTokenExDate(string? tokenExDate)
    {
        if (string.IsNullOrWhiteSpace(tokenExDate))
            return null;

        var digits = new string(tokenExDate.Where(char.IsDigit).ToArray());
        return digits.Length switch
        {
            4 => digits,
            6 => digits[^4..],
            // Cardcom TokenExDate is often YYYYMMDD (e.g. 20281101) — MM from month, YY from year.
            8 when digits.Length >= 8 => $"{digits.Substring(4, 2)}{digits.Substring(2, 2)}",
            _ => null,
        };
    }

    private static string? GetCardMonthProperty(JsonElement parent)
    {
        var month = GetInt32Property(parent, "CardMonth", -1);
        return month is >= 1 and <= 12 ? month.ToString("00") : null;
    }

    private static string? GetCardYearProperty(JsonElement parent)
    {
        var year = GetInt32Property(parent, "CardYear", -1);
        if (year < 0)
            return null;
        if (year < 100)
            return year.ToString("00");
        return year.ToString();
    }

    private static string? FormatLast4(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits.PadLeft(4, '0')[^4..];
    }

    private static readonly Regex CardcomTokenRegex = new(
        @"""Token""\s*:\s*""([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static string? FindCardcomTokenInJson(string json)
    {
        var match = CardcomTokenRegex.Match(json);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static readonly Regex CardcomApprovalRegex = new(
        @"""(?:ApprovalNumber|TokenApprovalNumber|AuthNum)""\s*:\s*""?([^"",}\s]+)""?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Fallback approval extraction from stored Cardcom JSON (e.g. J5 callback logged as 701).</summary>
    public static string? FindApprovalNumberInJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        var match = CardcomApprovalRegex.Match(json);
        if (!match.Success)
            return null;
        var value = match.Groups[1].Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>Cardcom charge tokens are UUID strings from Low Profile / TokenInfo.</summary>
    public static bool IsCardcomTokenUuid(string? token) =>
        !string.IsNullOrWhiteSpace(token) && Guid.TryParse(token.Trim(), out _);

    /// <summary>Safe token classification for logs (never logs the raw token).</summary>
    public static string DescribeTokenShape(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return "empty";
        var t = token.Trim();
        if (IsCardcomTokenUuid(t))
            return "uuid";
        if (t.StartsWith("{\"V\"", StringComparison.Ordinal) || t.Contains("\"Et\"", StringComparison.Ordinal))
            return "credential-json";
        if (t.StartsWith("{", StringComparison.Ordinal))
            return "json-other";
        if (t.StartsWith(PaymentTokenProtector.DatabaseKeyPrefix, StringComparison.Ordinal))
            return "encrypted-v2-blob";
        return $"other(len={t.Length})";
    }

    private static string? FindCardExpirationInJson(string json)
    {
        var monthMatch = Regex.Match(json, @"""CardMonth""\s*:\s*(\d{1,2})", RegexOptions.CultureInvariant);
        var yearMatch = Regex.Match(json, @"""CardYear""\s*:\s*(\d{2,4})", RegexOptions.CultureInvariant);
        if (!monthMatch.Success || !yearMatch.Success)
            return NormalizeCardExpirationFromTokenExDate(FindJsonStringValue(json, "TokenExDate"));

        var month = monthMatch.Groups[1].Value.PadLeft(2, '0');
        var yearDigits = yearMatch.Groups[1].Value;
        var yy = yearDigits.Length >= 2 ? yearDigits[^2..] : yearDigits;
        return $"{month}{yy}";
    }

    private static string? FindJsonStringValue(string json, string propertyName)
    {
        var match = Regex.Match(
            json,
            $@"""{Regex.Escape(propertyName)}""\s*:\s*""([^""]*)""",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? GetString(string json, string name)
    {
        using var doc = JsonDocument.Parse(json);
        return GetString(doc.RootElement, name);
    }
}
