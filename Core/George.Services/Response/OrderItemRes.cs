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

    /// <summary>True after ליקוט explicitly saved this line; false when quantity is only DB baseline for inventory.</summary>
    public bool PickingUserConfirmed { get; set; }
    /// <summary>פחת (%) folded into <see cref="TotalPrice"/> at picking; null when none.</summary>
    public decimal? DepreciationPercent { get; set; }
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
    /// <summary>total_weight | per_unit | units_only - for order line display without loading the catalog product.</summary>
    public string? WeightDisplayMode { get; set; }

    public string? OrderLineQuantityMode { get; set; }
    public string? OrderLinePerUnitWeightLabel { get; set; }
    public string? OrderLineSizeLabel { get; set; }
    public string? OrderLineCuttingLabel { get; set; }

    /// <summary>Typed display snapshot (JSON) - see <c>OrderLineDisplaySnapshot</c>; null on legacy lines.</summary>
    public string? LineDisplayJson { get; set; }

    // Sprint 4: promotion linkage
    public int? PromotionId { get; set; }
    public decimal? DiscountAmount { get; set; }
    /// <summary>Promotion display name when <see cref="PromotionId"/> is set.</summary>
    public string? PromotionName { get; set; }

    // Bundles (מארזים) - BUNDLES_SYNC_SPEC.md §3.3
    /// <summary>Parent line only: the bundle product id.</summary>
    public int? BundleProductId { get; set; }
    /// <summary>Child line only: id of the bundle parent line.</summary>
    public int? ParentOrderItemId { get; set; }
    /// <summary>Child line: the configured slot; null for lines of bundles George does not know.</summary>
    public int? BundleComponentId { get; set; }
    /// <summary>Child line: slot index in Woo's <c>_oc_bundle_components</c> (0-based).</summary>
    public int? BundleComponentIndex { get; set; }
    /// <summary>Child line: the configured component product when the slot was swapped.</summary>
    public int? SwappedFromProductId { get; set; }
    /// <summary>Display name of <see cref="SwappedFromProductId"/> (filled by the orders wave).</summary>
    public string? SwappedFromProductName { get; set; }

    /// <summary>Product.PrintName - printed on the order-entry voucher instead of the title (null = none).</summary>
    public string? ProductPrintName { get; set; }
    /// <summary>Child line: swap surcharge per bundle.</summary>
    public decimal? SwapSurcharge { get; set; }
    /// <summary>WooCommerce order item id (payload <c>itemId</c>).</summary>
    public int? WooLineItemId { get; set; }
    /// <summary>See <c>BundleOrderLines.IsBundleParent</c>.</summary>
    public bool IsBundleParent { get; set; }
    /// <summary>See <c>BundleOrderLines.IsBundleChild</c>.</summary>
    public bool IsBundleChild { get; set; }
    /// <summary>
    /// Child line, only while the order is still pickable (not Completed/Cancelled): the slot's configured swaps
    /// the picker may choose from. Null otherwise. Spec §8 "picking / order screens".
    /// </summary>
    public List<OrderItemBundleSwapOptionRes>? BundleSwapOptions { get; set; }
    /// <summary>The slot's "ניתן להחלפה" flag (single pickable order reads); null when the bundle has no definition in George.</summary>
    public bool? BundleSlotSwappable { get; set; }
    /// <summary>
    /// Bundle PARENT line (single pickable order reads): true when the bundle is priced by its components AND
    /// re-weighed on picking - the picking screen then shows the bundle price following the weighed quantities
    /// (same formula as the save, <c>BundleOrderLineBuilder.ApplyChildShares</c>).
    /// </summary>
    public bool? BundleReweighPrice { get; set; }
}

/// <summary>One configured swap of a bundle slot, as offered on the picking screen.</summary>
public class OrderItemBundleSwapOptionRes
{
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    public string? Name { get; set; }
    /// <summary>Per bundle.</summary>
    public decimal Surcharge { get; set; }
    /// <summary>The alternative's own quantity per bundle (its unit); null = the slot quantity converted by weight.</summary>
    public decimal? Qty { get; set; }
}
