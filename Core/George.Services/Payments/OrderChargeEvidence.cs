using George.Common.Payment;
using George.DB;

namespace George.Services.Payments;

/// <summary>
/// "Was this gateway credit order actually charged?" - one answer shared by the order update guard and the
/// revenue report. <c>Order.PaymentStatus = "Paid"</c> alone is NOT evidence: it is a free-form flag the client
/// sends ("סיום טיפול", the old "חויב טלפונית" button), and Hinnawi Jaffa #76/#79/#80 (Sept 2026) were
/// delivered, reported as income and never charged that way. Evidence is a settled gateway state or a successful
/// charge/capture event; Woo orders captured by the store plugin reach <c>Captured</c> through the webhook, so
/// they pass without George-side events.
/// </summary>
public static class OrderChargeEvidence
{
    /// <summary>Credit methods George charges through its own gateway integration (Cardcom / PayPlus).</summary>
    public static bool IsGatewayCreditOrder(Order order)
    {
        if (string.IsNullOrWhiteSpace(order.PaymentGateway)) return false;
        return IsGatewayCreditMethod(order.PaymentMethod);
    }

    public static bool IsGatewayCreditMethod(string? method)
    {
        var m = (method ?? "").Trim();
        return m.Equals("CreditCard", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditPhone", StringComparison.OrdinalIgnoreCase)
            || m.Equals("CreditSms", StringComparison.OrdinalIgnoreCase)
            || m.Equals("SavedCard", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Gateway state that only a real charge produces (a hold, a failed attempt or an opened page do not).</summary>
    public static bool HasSettledCharge(Order order)
    {
        var settle = (order.PaymentSettleStatus ?? "").Trim();
        return settle.Equals(PaymentSettleStatus.Captured, StringComparison.OrdinalIgnoreCase)
            || settle.Equals(PaymentSettleStatus.PartiallyCaptured, StringComparison.OrdinalIgnoreCase)
            || settle.Equals(PaymentSettleStatus.Refunded, StringComparison.OrdinalIgnoreCase)
            || settle.Equals(PaymentSettleStatus.PartiallyRefunded, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSuccessfulChargeEvent(OrderPaymentEvent e)
    {
        var type = (e.EventType ?? "").Trim();
        if (!type.Equals("ChargeToken", StringComparison.OrdinalIgnoreCase)
            && !type.Equals("CaptureAuthorization", StringComparison.OrdinalIgnoreCase))
            return false;
        var code = (e.StatusCode ?? "").Trim();
        return code == "0" || code == "000" || code.Equals("Success", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Paid flag on a gateway credit order with nothing behind it. <paramref name="hasSuccessfulChargeEvent"/> is
    /// the caller's lookup of <see cref="IsSuccessfulChargeEvent"/> over the order's events.
    /// </summary>
    public static bool IsPaidWithoutCharge(Order order, bool hasSuccessfulChargeEvent)
    {
        if (!string.Equals((order.PaymentStatus ?? "").Trim(), "Paid", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!IsGatewayCreditOrder(order)) return false;
        return !HasSettledCharge(order) && !hasSuccessfulChargeEvent;
    }

    /// <summary>
    /// Staff-facing refusal when a client tries to mark such an order Paid. Names the alternatives staff
    /// actually have (the hold, if there was one, is usually expired by the time this matters).
    /// </summary>
    public const string MarkPaidRefusedMessage =
        "ההזמנה לא חויבה בפועל - אין חיוב אשראי מוצלח. יש לחייב (אשראי ב-SMS / הזנת אשראי ידנית) "
        + "או להחליף את אמצעי התשלום למזומן / אשראי חיצוני / הקפה לפני סימון כשולם.";
}
