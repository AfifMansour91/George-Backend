namespace George.Services.Request;

public class SendPaymentSmsReq
{
    public string? OverridePhone { get; set; }
}

public class RefundPaymentReq
{
    public decimal? Amount { get; set; }
    public string? Reason { get; set; }
}

public class UpdateSitePaymentSettingsReq
{
    public string? PaymentGatewayProvider { get; set; }
    public int? CardcomTerminalNumber { get; set; }
    /// <summary>Second Cardcom terminal (no CVV) for token charges. 0 clears back to single-terminal.</summary>
    public int? CardcomChargeTerminalNumber { get; set; }
    public string? CardcomApiName { get; set; }
    public string? CardcomApiPassword { get; set; }
    public bool? CardcomSaveCardEnabled { get; set; }
    /// <summary>Max installments on the hosted payment page for immediate charges (1-36). 1 hides the selector.</summary>
    public int? CardcomMaxInstallments { get; set; }
    public int? PaymentAuthBufferPercent { get; set; }
    public decimal? PaymentMaxAuthAmount { get; set; }
    public bool? PaymentAllowCaptureAboveAuth { get; set; }
    public string? CardcomCssUrl { get; set; }
    public string? CardcomLogoUrl { get; set; }
}
