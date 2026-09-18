using System.Globalization;
using George.Data;

namespace George.Services.Bundles;

/// <summary>
/// What one slot unit of a product costs, exactly like a regular order line (shop-manager
/// <c>calculateLineTotal</c>): a product priced per kg but sold by units costs <c>price × unit weight</c> per unit.
/// <list type="bullet">
/// <item><c>by_weight</c> - the slot is measured in kg, the price is per kg → factor 1.</item>
/// <item><c>by_unit</c> / <c>by_unit_and_weight</c> - the slot counts units → factor = the unit's weight in kg:
/// <c>variable</c> = the weight chosen for the slot (else the first weight option), <c>by_variant</c> = the variant's
/// weight, otherwise the configured unit weight.</item>
/// <item>anything else (standard products) - priced per unit → factor 1.</item>
/// </list>
/// Spec: BUNDLES_SYNC_SPEC.md §4.
/// </summary>
public static class BundleSlotUnitWeight
{
    private const decimal DefaultUnitWeight = 0.5m;

    /// <summary>True when the product's weight is chosen from a list per slot (<c>by_unit</c> + <c>variable</c>).</summary>
    public static bool IsVariableWeight(BundleCatalogProductInfo? product) =>
        product != null
        && string.Equals(product.SetupType, "by_unit", StringComparison.OrdinalIgnoreCase)
        && string.Equals(product.UnitWeightMode, "variable", StringComparison.OrdinalIgnoreCase);

    /// <summary>Multiplier from the catalog price to the price of one slot unit (kg per unit, or 1).</summary>
    public static decimal PriceFactor(BundleCatalogProductInfo? product, BundleCatalogVariantInfo? variant, decimal? slotUnitWeightKg)
    {
        if (product == null) return 1m;
        var setup = (product.SetupType ?? "").Trim().ToLowerInvariant();
        if (setup != "by_unit" && setup != "by_unit_and_weight") return 1m;

        var unitIsGrams = string.Equals(product.WeightUnit?.Trim(), "g", StringComparison.OrdinalIgnoreCase);
        decimal ToKg(decimal w) => unitIsGrams ? w / 1000m : w;
        var unitWeightKg = ToKg(ParseDecimal(product.UnitWeight) is > 0m and var uw ? uw : DefaultUnitWeight);

        if (setup == "by_unit")
        {
            var mode = (product.UnitWeightMode ?? "").Trim().ToLowerInvariant();
            if (mode == "variable")
            {
                if (slotUnitWeightKg is > 0m) return slotUnitWeightKg.Value;
                var first = FirstWeightOption(product.WeightOptions);
                return first is > 0m ? ToKg(first.Value) : unitWeightKg;
            }
            if (mode == "by_variant" || product.WeightByVariant)
                return variant?.Weight is > 0m ? ToKg(variant.Weight.Value) : unitWeightKg;
        }
        return unitWeightKg;
    }

    /// <summary>First ACTIVE weight option (the part before "##" holds the active ones; separators "," / ";").</summary>
    public static decimal? FirstWeightOption(string? weightOptions)
    {
        if (string.IsNullOrWhiteSpace(weightOptions)) return null;
        var active = weightOptions;
        var cut = active.IndexOf("##", StringComparison.Ordinal);
        if (cut >= 0) active = active[..cut];
        foreach (var part in active.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var n = ParseDecimal(part);
            if (n is > 0m) return n;
        }
        return null;
    }

    private static decimal? ParseDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return decimal.TryParse(s.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }
}

/// <summary>
/// Resolved prices keyed by (product, variant). The VALUE is the price of ONE SLOT UNIT (per kg for kg slots, per
/// piece otherwise - see <see cref="BundleSlotUnitWeight"/>). For "choose a weight" products the dictionary value
/// uses the default weight; <see cref="SlotPrice"/> re-prices with the weight chosen for a specific slot.
/// </summary>
public sealed class BundlePriceBook : Dictionary<(int ProductId, int? VariantId), decimal>
{
    /// <summary>The catalog/site price as listed (per kg for weighable products), before the unit-weight factor.</summary>
    private readonly Dictionary<(int ProductId, int? VariantId), decimal> _catalog = new();
    /// <summary>"Choose a weight" products: re-priced with the weight chosen for a specific slot.</summary>
    private readonly HashSet<(int ProductId, int? VariantId)> _variableWeight = new();

    public void Set((int ProductId, int? VariantId) key, decimal catalogPrice, decimal slotUnitPrice, bool isVariableWeight)
    {
        this[key] = slotUnitPrice;
        _catalog[key] = catalogPrice;
        if (isVariableWeight) _variableWeight.Add(key); else _variableWeight.Remove(key);
    }

    /// <summary>The listed catalog/site price (per kg for weighable products) - what a kg line is priced with.</summary>
    public decimal CatalogPrice(int productId, int? variantId) =>
        _catalog.TryGetValue((productId, variantId), out var price) ? price : SlotPrice(productId, variantId, null);

    /// <summary>Price of one unit of the slot: the slot's chosen weight wins for "choose a weight" products.</summary>
    public decimal SlotPrice(int productId, int? variantId, decimal? slotUnitWeightKg)
    {
        var key = (productId, variantId);
        if (slotUnitWeightKg is > 0m && _variableWeight.Contains(key) && _catalog.TryGetValue(key, out var perKg))
            return perKg * slotUnitWeightKg.Value; // not rounded: the engine rounds the bundle total, like an order line
        return TryGetValue(key, out var price) ? price : 0m;
    }

    /// <summary>Price for a child order line: a kg line is always priced per kg, a units line per slot unit.</summary>
    public decimal LinePrice(int productId, int? variantId, bool isWeightLine, decimal? slotUnitWeightKg) =>
        isWeightLine ? CatalogPrice(productId, variantId) : SlotPrice(productId, variantId, slotUnitWeightKg);

    public void Merge(BundlePriceBook other)
    {
        foreach (var kv in other)
        {
            this[kv.Key] = kv.Value;
            if (other._catalog.TryGetValue(kv.Key, out var c)) _catalog[kv.Key] = c;
            if (other._variableWeight.Contains(kv.Key)) _variableWeight.Add(kv.Key); else _variableWeight.Remove(kv.Key);
        }
    }
}
