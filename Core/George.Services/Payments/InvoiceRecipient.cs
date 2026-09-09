using George.DB;

namespace George.Services.Payments;

/// <summary>
/// Who an invoice / credit note is made out to. Precedence:
/// 1. the customer's "חשבונית על שם אחר" business name (+ ח.פ) when set,
/// 2. the CRM customer's CURRENT name (a rename in the Customers page must reach the invoice - the order's
///    snapshot is what the shop saw at intake; Zano Dagim 2026-09-09: invoices kept printing the old name),
/// 3. the order's own customer name.
/// Requires <c>Order.Customer</c> to be loaded (PaymentStorage.GetOrderForPaymentAsync includes it);
/// without it the resolver falls back to the order snapshot.
/// </summary>
public static class InvoiceRecipient
{
    public static (string? Name, string? TaxId) Resolve(Order order)
    {
        var customer = order.Customer;
        if (customer != null && !customer.IsDeleted)
        {
            if (!string.IsNullOrWhiteSpace(customer.InvoiceName))
                return (customer.InvoiceName.Trim(), NullIfBlank(customer.InvoiceTaxId));
            if (!string.IsNullOrWhiteSpace(customer.Name))
                return (customer.Name.Trim(), null);
        }
        return (NullIfBlank(order.CustomerName), null);
    }

    private static string? NullIfBlank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
