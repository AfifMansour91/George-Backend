namespace George.Services.Payments.PayPlus;

/// <summary>PayPlus Invoice+ "books/docs/new/{doctype}" payload (analogue of CardcomTransactionDocument).</summary>
public sealed class PayPlusTransactionDocument
{
    /// <summary>PayPlus doc type path segment: inv_tax_receipt (tax invoice + receipt) or inv_refund (credit note).</summary>
    public string DocType { get; init; } = "inv_tax_receipt";
    public string? Name { get; init; }
    public string? TaxId { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine1 { get; init; }
    public string? City { get; init; }
    public bool SendByEmail { get; init; }
    public string Language { get; init; } = "he";
    public string CurrencyCode { get; init; } = "ILS";
    /// <summary>Links the document to the PayPlus transaction it settles (transaction_uuid).</summary>
    public string? TransactionUid { get; init; }
    /// <summary>Idempotency key so a retried request does not create a duplicate document.</summary>
    public string? UniqueIdentifier { get; init; }
    /// <summary>Invoice+ brand UID (issuing business) — required; PayPlus answers "brand-not-found" without it.</summary>
    public string? BrandUid { get; init; }
    public IReadOnlyList<PayPlusDocumentProductLine> Products { get; init; } = Array.Empty<PayPlusDocumentProductLine>();
}

public sealed class PayPlusDocumentProductLine
{
    public required string Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public bool IsVatFree { get; init; }
}

public sealed class CreatePayPlusDocumentRequest
{
    public required PayPlusTransactionDocument Document { get; init; }
}
