namespace George.Services.Response;

public class PaymentSessionRes
{
    public int OrderId { get; set; }
    public string? PaymentUrl { get; set; }
    public string? LowProfileId { get; set; }
    public decimal AuthorizedAmount { get; set; }
}

public class SendPaymentSmsRes
{
    public bool Sent { get; set; }
    public string? MaskedPhone { get; set; }
    public string? PaymentUrl { get; set; }
}

public class FinalizePickingPaymentRes
{
    public string Outcome { get; set; } = "";
    public decimal FinalAmount { get; set; }
    public decimal AuthorizedAmount { get; set; }
    public decimal TopupAmount { get; set; }
    public string? TransactionId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? DocumentUrl { get; set; }
}

public class OrderInvoiceRes
{
    public bool Success { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? DocumentUrl { get; set; }
    public string? Message { get; set; }
    public bool SmsSent { get; set; }
    public string? MaskedPhone { get; set; }
    public bool EmailSent { get; set; }
}

public class RefundPaymentRes
{
    public bool Success { get; set; }
    public decimal RefundedAmount { get; set; }
    public string? TransactionId { get; set; }
}

public class SavedCardRes
{
    public bool HasCard { get; set; }
    public string? Last4Digits { get; set; }
    public string? CardBrand { get; set; }
    public int? CustomerPaymentMethodId { get; set; }
}

public class PaymentEventRes
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public string EventType { get; set; } = "";
    public string Provider { get; set; } = "";
    public string? StatusCode { get; set; }
    public string? Description { get; set; }
    public string? GatewayTransactionId { get; set; }
    public string? MaskedToken { get; set; }
    public decimal? Amount { get; set; }
    public DateTime CreationTime { get; set; }
}

public class TestConnectionRes
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public class SitePaymentSettingsRes
{
    public int SiteId { get; set; }
    public string PaymentGatewayProvider { get; set; } = "none";
    public int? CardcomTerminalNumber { get; set; }
    public string? CardcomApiName { get; set; }
    public bool HasCardcomApiPassword { get; set; }
    public bool CardcomSaveCardEnabled { get; set; }
    public int PaymentAuthBufferPercent { get; set; }
    public decimal? PaymentMaxAuthAmount { get; set; }
    public bool PaymentAllowCaptureAboveAuth { get; set; }
    public string? CardcomCssUrl { get; set; }
    public string? CardcomLogoUrl { get; set; }
}
