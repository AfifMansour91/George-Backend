namespace George.Services.Marketing;

/// <summary>
/// The kind is decided where the message is SENT, not by reading its content (spec §8.0) - so there is never
/// an argument about whether a text was "operational". Operational and marketing are two separate wallets.
/// </summary>
public static class MessageKind
{
    /// <summary>Sent automatically around an order: confirmation, invoice, ready, refund...</summary>
    public const string Operational = "operational";
    public const string Marketing = "marketing";
    /// <summary>Platform messages that are never the shop's cost: login OTP, settings test.</summary>
    public const string System = "system";
}

public static class MessageCategory
{
    public const string OrderConfirmation = "order_confirmation";
    public const string ManagerAlert = "manager_alert";
    public const string OrderReady = "order_ready";
    public const string Reminder = "reminder";
    public const string PaymentLink = "payment_link";
    public const string Invoice = "invoice";
    public const string Refund = "refund";
    public const string Marketing = "marketing";
    public const string MarketingTest = "marketing_test";
    public const string Otp = "otp";
    public const string Test = "test";
}

public sealed class SmsLogContext
{
    public int? AccountId { get; init; }
    public int? SiteId { get; init; }
    public string Kind { get; init; } = MessageKind.Operational;
    public string Category { get; init; } = string.Empty;
    public int? OrderId { get; init; }
    public long? MarketingDeliveryId { get; init; }

    public static SmsLogContext Operational(string category, int accountId, int? siteId, int? orderId) =>
        new() { Kind = MessageKind.Operational, Category = category, AccountId = accountId, SiteId = siteId, OrderId = orderId };

    public static SmsLogContext System(string category, int? accountId = null) =>
        new() { Kind = MessageKind.System, Category = category, AccountId = accountId };
}
