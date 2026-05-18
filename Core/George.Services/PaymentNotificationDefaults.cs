namespace George.Services;

/// <summary>Default Hebrew SMS templates for payment notifications (KSP-style).</summary>
public static class PaymentNotificationDefaults
{
    public const string InvoiceSms =
        "התקבלה חשבונית מס וקבלה דיגיטליים מספר [invoice_number] מאת [store_name]: [document_url]";

    public const string RefundSms =
        "התקבל זיכוי מאת [store_name], לצפייה בסכום הזיכוי והחשבונית מס זיכוי דיגיטלית נא לפתוח את הקישור: [document_url]";

    public const string PaymentLinkSms =
        "לתשלום עבור הזמנה [order_number] מאת [store_name]: [payment_url]";
}
