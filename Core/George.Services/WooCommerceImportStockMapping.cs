namespace George.Services;

/// <summary>
/// WooCommerce → George stock classification during catalog import.
/// </summary>
public static class WooCommerceImportStockMapping
{
    public readonly record struct VariationStockSource(bool? ManageStock, decimal? StockQuantity, string? StockStatus);

    /// <summary>
    /// True when George should use <c>stock_management_type = variation</c> for this Woo variable product.
    /// </summary>
    public static bool UsesVariationStockManagement(IReadOnlyList<VariationStockSource> variations)
    {
        if (variations.Count == 0)
            return false;

        if (variations.Any(v => v.ManageStock == true))
            return true;

        var statuses = variations
            .Select(v => NormalizeStockStatusToken(v.StockStatus))
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return statuses.Count > 1;
    }

    /// <summary>
    /// Matches George <see cref="George.DB.Product.VariationStockByQuantity"/> — only when Woo tracks numeric qty per variation.
    /// </summary>
    public static bool VariationTracksQuantity(IReadOnlyList<VariationStockSource> variations) =>
        variations.Any(v => v.ManageStock == true);

  /// <summary>
    /// George stock management type name: <c>quantity</c> | <c>variation</c> | <c>status</c>.
    /// </summary>
    public static string ResolveStockManagementTypeName(
        bool? parentManageStock,
        bool isVariable,
        IReadOnlyList<VariationStockSource> variations)
    {
        if (parentManageStock == true)
            return "quantity";

        if (isVariable && variations.Count > 0 && UsesVariationStockManagement(variations))
            return "variation";

        return "status";
    }

    /// <summary>
    /// George stores per-variation salability as <c>StockQuantity &gt; 0</c> (binary 0/1 or real qty).
    /// Returns null when parent does not use variation stock management.
    /// </summary>
    public static decimal? ResolveVariantStockQuantity(VariationStockSource variation, bool usesVariationStock)
    {
        if (!usesVariationStock)
            return null;

        if (variation.ManageStock == true && variation.StockQuantity.HasValue)
            return variation.StockQuantity.Value;

        var st = NormalizeStockStatusToken(variation.StockStatus);
        if (st is "instock" or "onbackorder")
            return 1m;
        if (st is "outofstock")
            return 0m;
        return null;
    }

    public static bool? ResolveVariationStockByQuantity(IReadOnlyList<VariationStockSource> variations) =>
        variations.Count > 0 ? VariationTracksQuantity(variations) : null;

    private static string NormalizeStockStatusToken(string? stockStatus)
    {
        if (string.IsNullOrWhiteSpace(stockStatus))
            return string.Empty;
        return stockStatus.Trim().ToLowerInvariant().Replace("-", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal);
    }
}
