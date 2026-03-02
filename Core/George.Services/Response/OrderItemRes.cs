namespace George.Services.Response;

/// <summary>Sprint 2: Order line item response.</summary>
public class OrderItemRes
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int? ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? Title { get; set; }
    public string? VariantTitle { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitWeightGrams { get; set; }
    public decimal? PricePerUnit { get; set; }
    public decimal? TotalPrice { get; set; }
    /// <summary>Picked quantity/weight from picking flow; null when not yet picked.</summary>
    public decimal? PickedQuantity { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}
