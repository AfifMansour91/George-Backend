namespace George.Services.Payments.PayPlus;

/// <summary>
/// Maps <see cref="PayPlusTransactionDocument"/> to the "books/docs/new/{doctype}" JSON body, per
/// https://docs.payplus.co.il/reference/post_books-docs-new-doctype.md (fields confirmed against docs,
/// not yet exercised against the sandbox - verify field names end-to-end before relying on this in production).
/// </summary>
internal static class PayPlusDocumentPayload
{
    public static Dictionary<string, object?> ToDictionary(PayPlusTransactionDocument doc)
    {
        var items = doc.Products.Select(p =>
        {
            var line = new Dictionary<string, object?>
            {
                ["name"] = p.Description,
                ["quantity"] = p.Quantity,
                ["price"] = p.UnitCost,
                ["currency_code"] = doc.CurrencyCode,
                // Catalog prices are gross (Israel); say so per line rather than trusting the account default.
                ["vat_type_code"] = p.IsVatFree ? "vat-type-exempt" : "vat-type-included",
            };
            return line;
        }).ToList();

        // Invoice+ rejects a document without its total (PEPE 9/9: "missing-totalAmount-param"). The total is
        // the payment recorded when there is one (receipt-type docs), else the sum of the lines.
        var linesTotal = Math.Round(doc.Products.Sum(p => p.UnitCost * p.Quantity), 2, MidpointRounding.AwayFromZero);
        var totalAmount = doc.PaymentAmount is > 0m ? doc.PaymentAmount.Value : linesTotal;

        var customer = new Dictionary<string, object?>
        {
            ["name"] = doc.Name,
        };
        if (!string.IsNullOrWhiteSpace(doc.Email))
            customer["email"] = doc.Email;
        if (!string.IsNullOrWhiteSpace(doc.Phone))
            customer["phone"] = doc.Phone;
        if (!string.IsNullOrWhiteSpace(doc.TaxId))
            customer["vat_number"] = doc.TaxId;
        if (!string.IsNullOrWhiteSpace(doc.AddressLine1))
            customer["street_name"] = doc.AddressLine1;
        if (!string.IsNullOrWhiteSpace(doc.City))
            customer["city"] = doc.City;

        var body = new Dictionary<string, object?>
        {
            ["language"] = doc.Language,
            ["currency_code"] = doc.CurrencyCode,
            ["vatType"] = "vat-type-included",
            ["customer"] = customer,
            ["items"] = items,
            ["totalAmount"] = totalAmount,
            ["send_document_email"] = doc.SendByEmail,
        };

        if (!string.IsNullOrWhiteSpace(doc.TransactionUid))
            body["transaction_uuid"] = doc.TransactionUid;
        if (!string.IsNullOrWhiteSpace(doc.UniqueIdentifier))
            body["unique_identifier"] = doc.UniqueIdentifier;
        if (!string.IsNullOrWhiteSpace(doc.BrandUid))
            body["brand_uuid"] = doc.BrandUid;

        // Receipt-type documents must carry the payment they record (`payments`), else 162 missing-payment-information.
        if (doc.PaymentAmount is > 0m && !string.Equals(doc.DocType, "inv_tax", StringComparison.OrdinalIgnoreCase))
        {
            var payment = new Dictionary<string, object?>
            {
                ["payment_type"] = "credit-card",
                ["amount"] = doc.PaymentAmount.Value,
                ["payment_date"] = (doc.PaymentDate ?? DateTime.UtcNow).ToString("yyyy-MM-dd"),
                ["currency_code"] = doc.CurrencyCode,
                ["card_type"] = MapCardType(doc.CardBrand),
            };
            if (!string.IsNullOrWhiteSpace(doc.CardLast4))
                payment["four_digits"] = doc.CardLast4;
            if (doc.Installments > 1)
            {
                var first = Math.Round(doc.PaymentAmount.Value / doc.Installments, 2, MidpointRounding.AwayFromZero);
                payment["transaction_type"] = "payments";
                payment["payments"] = doc.Installments;
                payment["first_payment"] = Math.Round(doc.PaymentAmount.Value - first * (doc.Installments - 1), 2, MidpointRounding.AwayFromZero);
                payment["subsequent_payments"] = first;
            }
            else
            {
                payment["transaction_type"] = "normal";
            }
            body["payments"] = new List<Dictionary<string, object?>> { payment };
        }

        return body;
    }

    /// <summary>Invoice+ card_type enum from the brand names PayPlus itself reports (brand_name / clearing_name).</summary>
    private static string MapCardType(string? brand)
    {
        var b = (brand ?? "").Trim().ToLowerInvariant();
        if (b.Contains("master")) return "mastercard";
        if (b.Contains("visa")) return "visa";
        if (b.Contains("amex") || b.Contains("american")) return "american-express";
        if (b.Contains("diners")) return "diners";
        if (b.Contains("discover")) return "discover";
        if (b.Contains("jcb")) return "jcb";
        if (b.Contains("maestro")) return "maestro";
        return "other";
    }
}
