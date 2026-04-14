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
    public string? SaleUnits { get; set; }
    public string? SaleTotalWeight { get; set; }
    public int? WooCommerceProductId { get; set; }
    public int? WooCommerceVariationId { get; set; }
    public string? LineSku { get; set; }
    public string? LineQuantityType { get; set; }
    public decimal? LineUnit { get; set; }
    public decimal? LineUnitWeightKg { get; set; }
    public string? SaleUnitsLine { get; set; }
    public string? LinePayloadJson { get; set; }
    public int SortOrder { get; set; }
    /// <summary>total_weight | per_unit | units_only — for order line display without loading the catalog product.</summary>
    public string? WeightDisplayMode { get; set; }

    public string? OrderLineQuantityMode { get; set; }
    public string? OrderLinePerUnitWeightLabel { get; set; }
    public string? OrderLineSizeLabel { get; set; }
    public string? OrderLineCuttingLabel { get; set; }
}
