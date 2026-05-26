namespace George.Services.Payments.Cardcom;

/// <summary>Cardcom Do Transaction / CreateDocument payload (object 15 in API docs).</summary>
public sealed class CardcomTransactionDocument
{
    public string DocumentTypeToCreate { get; init; } = "TaxInvoiceAndReceipt";
    public string? Name { get; init; }
    public string? TaxId { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public bool SendByEmail { get; init; }
    public bool SendBySms { get; init; }
    public string? Comments { get; init; }
    public string? ExternalId { get; init; }
    public string Language { get; init; } = "he";
    /// <summary>Cardcom currency code (1 = ILS, 2 = USD).</summary>
    public int IsoCoinId { get; init; } = 1;
    public IReadOnlyList<CardcomDocumentProductLine> Products { get; init; } = Array.Empty<CardcomDocumentProductLine>();
}

public sealed class CardcomDocumentProductLine
{
    public string? ProductId { get; init; }
    public required string Description { get; init; }
    public decimal Quantity { get; init; }
    public decimal UnitCost { get; init; }
    public decimal? TotalLineCost { get; init; }
    public bool IsVatFree { get; init; }
}

public sealed class CreateCardcomDocumentRequest
{
    public required CardcomTransactionDocument Document { get; init; }
    /// <summary>Link document to an existing charge (optional).</summary>
    public string? TranzactionId { get; init; }
}
