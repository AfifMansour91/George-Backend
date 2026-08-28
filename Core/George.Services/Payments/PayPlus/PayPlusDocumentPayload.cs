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
            };
            if (p.IsVatFree)
                line["vat_type_code"] = "vat-type-exempt";
            return line;
        }).ToList();

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
            ["customer"] = customer,
            ["items"] = items,
            ["send_document_email"] = doc.SendByEmail,
        };

        if (!string.IsNullOrWhiteSpace(doc.TransactionUid))
            body["transaction_uuid"] = doc.TransactionUid;
        if (!string.IsNullOrWhiteSpace(doc.UniqueIdentifier))
            body["unique_identifier"] = doc.UniqueIdentifier;
        if (!string.IsNullOrWhiteSpace(doc.BrandUid))
            body["brand_uuid"] = doc.BrandUid;

        return body;
    }
}
