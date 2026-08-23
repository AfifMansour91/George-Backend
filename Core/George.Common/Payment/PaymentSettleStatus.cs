using System;

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

/// <summary>
/// Who charges a website (WooCommerce) order's card after picking. Null/<see cref="Plugin"/>: the store's
/// Cardcom gateway plugin captures when the order reaches "completed" and reports back by webhook.
/// <see cref="Giorgio"/>: the plugin handed the Cardcom token to Giorgio at checkout; Giorgio charges at
/// picking (same path as phone orders) and pushes the payment result to the store.
/// </summary>
public static class PaymentCaptureOwner
{
    public const string Plugin = "Plugin";
    public const string Giorgio = "Giorgio";

    public static bool IsGiorgio(string? value) =>
        string.Equals(value?.Trim(), Giorgio, StringComparison.OrdinalIgnoreCase);
}

public static class PaymentGatewayProviderId
{
    public const string None = "none";
    public const string Cardcom = "cardcom";
}
