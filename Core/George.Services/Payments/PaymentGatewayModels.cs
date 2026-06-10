using George.Services.Payments.Cardcom;

namespace George.Services.Payments;

public sealed class PaymentGatewayCapabilities
{
    public bool SupportsHostedSession { get; init; } = true;
    public bool SupportsTokenCharge { get; init; } = true;
    public bool SupportsCaptureAuthorization { get; init; } = true;
    public bool SupportsPartialRefund { get; init; } = true;
    public bool SupportsVoidAuthorization { get; init; } = true;
    public bool SupportsMotoPortal { get; init; }
    public bool SupportsCaptureAboveAuth { get; init; }
}

public sealed class CreateHostedSessionRequest
{
    public required int OrderId { get; init; }
    public required decimal Amount { get; init; }
    public required string ReturnValue { get; init; }
    public string? ProductName { get; init; }
    public string Language { get; init; } = "he";
    public bool SaveCard { get; init; }
    public bool UseAuthorizationHold { get; init; } = true;
    /// <summary>Manager MOTO: Cardcom Virtual Terminal iframe (card entry by staff).</summary>
    public bool UseVirtualTerminal { get; init; }
    public string? SuccessRedirectUrl { get; init; }
    public string? FailedRedirectUrl { get; init; }
    public string? WebHookUrl { get; init; }
    public string? CustomerName { get; init; }
    public string? CustomerPhone { get; init; }
    public string? CustomerEmail { get; init; }
}

public sealed class CreateHostedSessionResult
{
    public bool Success { get; init; }
    public string? PaymentUrl { get; init; }
    public string? LowProfileId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorDescription { get; init; }
    public string? RawJson { get; init; }
}

public sealed class ValidateCallbackRequest
{
    public required string LowProfileId { get; init; }
    public IReadOnlyDictionary<string, string>? ExtraParams { get; init; }
}

/// <summary>Card display metadata extracted from Cardcom JSON (GetLpResult / callback).</summary>
public sealed class CardcomCardDisplayFields
{
    public string? Last4Digits { get; init; }
    public string? CardBrand { get; init; }
    public string? TokenExDate { get; init; }
    public string? CardExpirationMMYY { get; init; }

    public bool HasDisplay =>
        !string.IsNullOrWhiteSpace(Last4Digits) || !string.IsNullOrWhiteSpace(CardBrand);
}

public sealed class ValidateCallbackResult
{
    public bool Success { get; init; }
    /// <summary>GetLpResult before customer finished (e.g. "עסקה ממתינה") — do not mark order failed.</summary>
    public bool IsPending { get; init; }
    public int ResponseCode { get; init; }
    public string? Description { get; init; }
    public string? ReturnValue { get; init; }
    public string? Operation { get; init; }
    public string? TranzactionId { get; init; }
    public string? SuspendedDealId { get; init; }
    public string? ApprovalNumber { get; init; }
    public string? Token { get; init; }
    public string? TokenExDate { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string? Last4Digits { get; init; }
    public string? CardBrand { get; init; }
    public string? DocumentNumber { get; init; }
    public string? DocumentUrl { get; init; }
    public decimal? Amount { get; init; }
    public string? RawJson { get; init; }
}

public sealed class CaptureAuthorizationRequest
{
    public required decimal Amount { get; init; }
    public string? Token { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string? ApprovalNumber { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
    public bool CreateDocument { get; init; } = true;
    public CardcomTransactionDocument? Document { get; init; }
}

/// <summary>J5 authorization hold via Do Transaction + token (e.g. saved card at phone order).</summary>
public sealed class PlaceTokenAuthorizationHoldRequest
{
    public required decimal Amount { get; init; }
    public required string Token { get; init; }
    public required string CardExpirationMMYY { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class ChargeTokenRequest
{
    public required decimal Amount { get; init; }
    public required string Token { get; init; }
    public required string CardExpirationMMYY { get; init; }
    public string? ApprovalNumber { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
    public bool CreateDocument { get; init; } = true;
    public string? CustomerName { get; init; }
    public CardcomTransactionDocument? Document { get; init; }
}

public sealed class RefundRequest
{
    public required decimal Amount { get; init; }
    public string? OriginalTranzactionId { get; init; }
    public string? Token { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class VoidAuthorizationRequest
{
    public required decimal Amount { get; init; }
    public required string Token { get; init; }
    public required string CardExpirationMMYY { get; init; }
    public required string ApprovalNumber { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class PaymentTransactionResult
{
    public bool Success { get; init; }
    public int ResponseCode { get; init; }
    public string? Description { get; init; }
    public string? TranzactionId { get; init; }
    public string? ApprovalNumber { get; init; }
    public string? DocumentNumber { get; init; }
    public string? DocumentUrl { get; init; }
    public string? RawJson { get; init; }
}

public sealed class TestConnectionResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
}

/// <summary>Cardcom Transactions/GetTransactionInfoById inquiry.</summary>
public sealed class CardcomTransactionInfoResult
{
    public bool Success { get; init; }
    public int ResponseCode { get; init; }
    public string? Description { get; init; }
    public string? TranzactionId { get; init; }
    public decimal? Amount { get; init; }
    public string? DealType { get; init; }
    public bool? IsRefund { get; init; }
    public string? RawJson { get; init; }
    public bool IsFinalCharge { get; init; }
    public bool IsAuthorizationHold { get; init; }
}

public sealed class SitePaymentCredentials
{
    public int SiteId { get; init; }
    public string ProviderId { get; init; } = "none";
    public int? TerminalNumber { get; init; }
    public string? ApiName { get; init; }
    public string? ApiPassword { get; init; }
    public bool ApiPasswordStoredButUnreadable { get; init; }
    public bool SaveCardEnabled { get; init; } = true;
    public int AuthBufferPercent { get; init; } = 25;
    public decimal? MaxAuthAmount { get; init; }
    public bool AllowCaptureAboveAuth { get; init; }
    public string? CssUrl { get; init; }
    public string? LogoUrl { get; init; }
    /// <summary>Optional JSON merged into LowProfile/Create (UIDefinition, AdvancedDefinition, etc.).</summary>
    public string? ProviderExtrasJson { get; init; }
    /// <summary>Cardcom document type (e.g. TaxInvoiceAndReceipt). Override via ProviderExtrasJson key cardcomDocumentType.</summary>
    public string DocumentTypeToCreate { get; init; } = CardcomDocumentBuilder.DefaultDocumentType;
    /// <summary>Send invoice link via internal SMS after successful capture (ProviderExtrasJson: cardcomSendInvoiceSmsAfterCapture).</summary>
    public bool SendInvoiceSmsAfterCapture { get; init; } = true;
    public string Currency { get; init; } = "ILS";
}
