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
    /// <summary>Invoice+ brand UID (issuing business) - required; PayPlus answers "brand-not-found" without it.</summary>
    public string? BrandUid { get; init; }
    public IReadOnlyList<PayPlusDocumentProductLine> Products { get; init; } = Array.Empty<PayPlusDocumentProductLine>();

    /// <summary>
    /// The payment the receipt half of an inv_tax_receipt records. Without a `payments` entry PayPlus
    /// rejects the document with error 162 "missing-payment-information" (PEPE 9/8: every capture logged it).
    /// </summary>
    public decimal? PaymentAmount { get; init; }
    public DateTime? PaymentDate { get; init; }
    public string? CardBrand { get; init; }
    public string? CardLast4 { get; init; }
    public int Installments { get; init; } = 1;
}

/// <summary>
/// How an Invoice+ document carries a discount (payment below the sum of the lines). PayPlus documents
/// nothing about how it computes the "calculated total" it checks <c>totalAmount</c> against, so the
/// shapes are tried in this order until PayPlus accepts one (see PaymentService.CreatePayPlusInvoiceDocumentAsync).
/// </summary>
public enum PayPlusDiscountShape
{
    /// <summary>Per-line <c>discount_type=amount</c>/<c>discount_value</c> - the vendor WooCommerce plugin's default shape.</summary>
    PerLineDiscount,
    /// <summary>One "הנחה" line with a negative price (the vendor plugin's "coupon as product" shape; also the Cardcom document shape).</summary>
    DiscountLine,
    /// <summary>Discount folded into the unit prices (rounded down) plus a small positive "התאמת סכום" line - only positive lines, no discount fields.</summary>
    NetUnitPrices,
}

public sealed class PayPlusDocumentProductLine
{
    public required string Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public bool IsVatFree { get; init; }
    /// <summary>
    /// Line-level discount in currency (Invoice+ <c>discount_type=amount</c> / <c>discount_value</c>, the same
    /// field the vendor WooCommerce plugin fills from subtotal − total). The line settles at
    /// Quantity × UnitCost − DiscountAmount.
    /// </summary>
    public decimal DiscountAmount { get; init; }

    public decimal NetTotal => Math.Round(Quantity * UnitCost, 2, MidpointRounding.AwayFromZero) - DiscountAmount;
}

public sealed class CreatePayPlusDocumentRequest
{
    public required PayPlusTransactionDocument Document { get; init; }
}
