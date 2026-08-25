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
    /// <summary>Max installments offered on the hosted page. Only honored for immediate charges — holds force 1.</summary>
    public int MaxInstallments { get; init; } = 1;
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

/// <summary>
/// Card display metadata extracted from a gateway's JSON (Cardcom GetLpResult/callback, or PayPlus
/// generateLink/IPN/View responses — content is provider-neutral, both gateways populate this same shape).
/// </summary>
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
    /// <summary>Installments the customer selected on the hosted page (TranzactionInfo). Null/1 = single payment.</summary>
    public int? NumOfPayments { get; init; }
    public string? RawJson { get; init; }
}

/// <summary>Cardcom Transactions/Transaction card-owner fields (must match Low Profile UIDefinition prefill).</summary>
public sealed class CardcomCardOwnerContact
{
    public string? Name { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
}

public sealed class CaptureAuthorizationRequest
{
    public required decimal Amount { get; init; }
    public string? Token { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string? ApprovalNumber { get; init; }
    /// <summary>PayPlus: the transaction_uid returned by the original authorization — captured via the same id.</summary>
    public string? ProviderTransactionId { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
    public bool CreateDocument { get; init; } = true;
    /// <summary>Installments for the charge (customer's hosted-page selection). 1 = single payment.</summary>
    public int NumOfPayments { get; init; } = 1;
    public CardcomCardOwnerContact? CardOwner { get; init; }
    public CardcomTransactionDocument? Document { get; init; }
}

/// <summary>J5 authorization hold via Do Transaction + token (e.g. saved card at phone order).</summary>
public sealed class PlaceTokenAuthorizationHoldRequest
{
    public required decimal Amount { get; init; }
    public required string Token { get; init; }
    /// <summary>Cardcom only — PayPlus has no separate expiry pair for a saved token.</summary>
    public string? CardExpirationMMYY { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class ChargeTokenRequest
{
    public required decimal Amount { get; init; }
    public required string Token { get; init; }
    /// <summary>Cardcom only — PayPlus has no separate expiry pair for a saved token.</summary>
    public string? CardExpirationMMYY { get; init; }
    public string? ApprovalNumber { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
    public bool CreateDocument { get; init; } = true;
    /// <summary>Installments for the charge (customer's hosted-page selection). 1 = single payment.</summary>
    public int NumOfPayments { get; init; } = 1;
    public CardcomCardOwnerContact? CardOwner { get; init; }
    public CardcomTransactionDocument? Document { get; init; }
}

public sealed class RefundRequest
{
    public required decimal Amount { get; init; }
    /// <summary>Cardcom: numeric internal deal number. PayPlus: the transaction_uid (GUID string) to refund.</summary>
    public string? OriginalTranzactionId { get; init; }
    public string? Token { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string ExternalUniqTranId { get; init; } = Guid.NewGuid().ToString("N");
}

public sealed class VoidAuthorizationRequest
{
    public required decimal Amount { get; init; }
    /// <summary>Cardcom only — required to void via Transactions/Transaction (MTI=420).</summary>
    public string? Token { get; init; }
    public string? CardExpirationMMYY { get; init; }
    public string? ApprovalNumber { get; init; }
    /// <summary>PayPlus: the transaction_uid to cancel via Transactions/Cancel.</summary>
    public string? ProviderTransactionId { get; init; }
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

/// <summary>
/// Transaction inquiry result (Cardcom Transactions/GetTransactionInfoById, or PayPlus Transactions/View —
/// content is provider-neutral, both gateways map into this same shape so <see cref="GatewayChargeVerification"/>
/// can compare either provider's answer with one set of rules).
/// </summary>
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
    /// <summary>Invoice document created with the transaction (e.g. by the Woo checkout) — used to send the invoice SMS for website orders.</summary>
    public string? DocumentNumber { get; init; }
    public string? DocumentUrl { get; init; }
}

public sealed class SitePaymentCredentials
{
    public int SiteId { get; init; }
    public string ProviderId { get; init; } = "none";
    public int? TerminalNumber { get; init; }

    /// <summary>PayPlus: the Payment Page UID (its per-site identifier — no int terminal number concept).</summary>
    public string? PaymentPageUid { get; init; }

    /// <summary>PayPlus: selects the sandbox (restapidev) vs production (restapi) base URL. Unused by Cardcom.</summary>
    public bool TestMode { get; init; }

    /// <summary>
    /// Optional second Cardcom terminal (configured WITHOUT a CVV requirement) used ONLY for the actual charge
    /// (J4 capture / direct token charge) and its refund. Token creation, holds (J5), voids and the hosted
    /// payment page stay on <see cref="TerminalNumber"/>.
    /// </summary>
    public int? ChargeTerminalNumber { get; init; }

    /// <summary>Terminal for the actual charge: the no-CVV charge terminal when configured, else the primary.</summary>
    public int? EffectiveChargeTerminalNumber => ChargeTerminalNumber is > 0 ? ChargeTerminalNumber : TerminalNumber;

    public string? ApiName { get; init; }
    public string? ApiPassword { get; init; }
    public bool ApiPasswordStoredButUnreadable { get; init; }
    public bool SaveCardEnabled { get; init; } = true;
    /// <summary>Max installments on the hosted payment page for immediate charges. 1 = selector hidden.</summary>
    public int MaxInstallments { get; init; } = 1;
    public int AuthBufferPercent { get; init; } = 25;
    public decimal? MaxAuthAmount { get; init; }
    public bool AllowCaptureAboveAuth { get; init; }
    public string? CssUrl { get; init; }
    public string? LogoUrl { get; init; }
    /// <summary>Optional JSON merged into LowProfile/Create (UIDefinition, AdvancedDefinition, etc.).</summary>
    public string? ProviderExtrasJson { get; init; }
    /// <summary>PayPlus Invoice+ brand UID — required for books/docs/* document creation ("brand-not-found" without it).</summary>
    public string? InvoiceBrandUid { get; init; }
    /// <summary>Cardcom document type (e.g. TaxInvoiceAndReceipt). Override via ProviderExtrasJson key cardcomDocumentType.</summary>
    public string DocumentTypeToCreate { get; init; } = CardcomDocumentBuilder.DefaultDocumentType;
    /// <summary>Send invoice link via internal SMS after successful capture (ProviderExtrasJson: cardcomSendInvoiceSmsAfterCapture).</summary>
    public bool SendInvoiceSmsAfterCapture { get; init; } = true;
    public string Currency { get; init; } = "ILS";
}
