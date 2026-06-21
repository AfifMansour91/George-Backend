namespace George.Common.Payment;

/// <summary>Order payment lifecycle (provider-agnostic).</summary>
public static class PaymentSettleStatus
{
    public const string None = "None";
    public const string Initiated = "Initiated";
    public const string Authorized = "Authorized";
    public const string Captured = "Captured";
    public const string PartiallyCaptured = "PartiallyCaptured";
    public const string OverAuthRequiresTopup = "OverAuthRequiresTopup";
    public const string Failed = "Failed";
    public const string Voided = "Voided";
    public const string Refunded = "Refunded";
    public const string PartiallyRefunded = "PartiallyRefunded";
}

public static class PaymentGatewayProviderId
{
    public const string None = "none";
    public const string Cardcom = "cardcom";
}
