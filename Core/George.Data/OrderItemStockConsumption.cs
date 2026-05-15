using George.DB;

namespace George.Data;

/// <summary>
/// Maps order line quantities / picking deltas to catalog stock units (e.g. kg for fixed-weight slice variants).
/// Keeps <see cref="OrderItem.PickedQuantity"/> and picking deltas aligned with what <see cref="ProductStorage.ApplyPickingConsumptionDeltaAsync"/> subtracts.
/// </summary>
public static class OrderItemStockConsumption
{
    /// <summary>
    /// True when picking UI stores picked amount in kg (same rules as legacy picking baseline in OrderStorage).
    /// </summary>
    public static bool LinePickingStoresKg(OrderItem item)
    {
        if (item.UnitWeightGrams is > 0m) return true;
        return string.Equals(item.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Per-unit weight in kg for lines sold by count (e.g. 200g slice). Returns null when quantity is already total kg (weight mode) or unknown.
    /// </summary>
    public static decimal? TryGetUnitWeightKg(OrderItem line)
    {
        if (line.LineUnitWeightKg is > 0m) return line.LineUnitWeightKg;
        if (line.UnitWeightGrams is > 0m)
        {
            // Matches OrderLineDisplayFieldsBuilder: 1000g sentinel = "by kg" line, not grams-per-piece.
            if (line.UnitWeightGrams == 1000m) return null;
            return line.UnitWeightGrams.Value / 1000m;
        }

        return null;
    }

    /// <summary>
    /// Stock to consume for the ordered line (before picking adjustments).
    /// Weight mode: prefer <c>TotalPrice / PricePerUnit</c> when both set (ordered kg); else <see cref="OrderItem.Quantity"/> as kg.
    /// Units with known grams per unit: <c>Quantity × unitKg</c>.
    /// </summary>
    public static decimal ResolveOrderedCatalogConsumption(OrderItem line)
    {
        if (line.Quantity <= 0m) return 0m;
        if (string.Equals(line.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase))
            return ResolveWeightModeOrderedKg(line);
        var unitKg = TryGetUnitWeightKg(line);
        if (unitKg is > 0m) return line.Quantity * unitKg.Value;
        return line.Quantity;
    }

    private static decimal ResolveWeightModeOrderedKg(OrderItem line)
    {
        if (line.TotalPrice is > 0m && line.PricePerUnit is > 0m)
        {
            var kg = line.TotalPrice.Value / line.PricePerUnit.Value;
            if (kg is > 0m and < 500m) return kg;
        }

        return line.Quantity;
    }

    /// <summary>
    /// Net catalog consumption implied by a picking save. When the picking UI is in kg, <paramref name="newPicked"/> / <paramref name="oldPicked"/> are already kg deltas-compatible.
    /// Otherwise piece deltas are scaled by per-unit kg when applicable.
    /// </summary>
    public static decimal ResolvePickingDeltaCatalogConsumption(OrderItem line, decimal oldPicked, decimal newPicked)
    {
        var delta = newPicked - oldPicked;
        if (delta == 0m) return 0m;
        if (LinePickingStoresKg(line)) return delta;
        var unitKg = TryGetUnitWeightKg(line);
        if (unitKg is > 0m) return delta * unitKg.Value;
        return delta;
    }
}
