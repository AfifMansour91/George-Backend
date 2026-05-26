using George.DB;

namespace George.Services;

/// <summary>
/// Maps catalog stock to ok | low | out — same rules as the SPA <c>getProductStockFilterBucket</c>
/// in <c>src/lib/stockLevels.ts</c> (My Products stock filters). Shared by products report and inventory report.
/// </summary>
public static class ProductCatalogStockClassification
{
    public static string ClassifyStock(Product p, Account? account)
    {
        if (IsOnBackorderStatus(p))
            return "ok";

        if (IsVariableUnitWeightProduct(p))
            return ClassifyVariableUnitWeightStock(p);

        var activeVariants = ActiveVariants(p);
        if (IsVariationManagement(p) && activeVariants.Count > 0)
            return ClassifyStockVariation(p, account, activeVariants);

        if (IsStatusOnlyManagement(p))
            return IsOutOfStockStatus(p) ? "out" : "ok";

        // Unspecified / legacy management with no stored quantity: follow StockStatus (same idea as SPA
        // isProductInventoryAvailableForSale — do not treat null quantity as 0 units unless quantity mode or a numeric row exists).
        if (!UsesQuantityThresholds(p))
            return IsOutOfStockStatus(p) ? "out" : "ok";

        if (IsOutOfStockStatus(p))
            return "out";

        var qty = p.StockQuantity ?? 0m;
        if (qty <= 0m)
            return "out";

        var thr = ResolveLowThreshold(p, account);
        if (thr > 0m && qty <= thr)
            return "low";

        return "ok";
    }

    /// <summary>Stock status for a products-report cut / variation row (resolved variant id when known).</summary>
    public static string ClassifyCutRowStock(Product p, Account? account, int? variantId)
    {
        if (variantId is > 0)
        {
            var v = ActiveVariants(p).FirstOrDefault(x => x.Id == variantId);
            if (v != null)
                return ClassifySingleVariant(p, v, account);
        }

        return ClassifyStock(p, account);
    }

    /// <summary>Per-variation status when parent uses variation stock (aligned with <see cref="ClassifyStockVariation"/>).</summary>
    public static string ClassifySingleVariant(Product p, ProductVariant v, Account? account)
    {
        if (!IsVariationManagement(p))
            return ClassifyStock(p, account);

        var byQty = p.VariationStockByQuantity == true;
        if (byQty)
        {
            var threshold = ResolveLowThreshold(p, account);
            var s = LineStateForQty(v.StockQuantity ?? 0m, threshold);
            return s == LineState.Out ? "out" : s == LineState.Low ? "low" : "ok";
        }

        return (v.StockQuantity ?? 0m) > 0m ? "ok" : "out";
    }

    public static decimal ResolveLowThreshold(Product p, Account? account)
    {
        if (p.LowStockThreshold is > 0m) return p.LowStockThreshold.Value;
        var weighted = IsWeightedLikeProduct(p);
        if (account != null)
        {
            var d = weighted ? account.DefaultLowStockThresholdWeighted : account.DefaultLowStockThresholdUnits;
            if (d is > 0m) return d.Value;
        }

        // Match SPA `FALLBACK_STOCK_THRESHOLDS` in src/lib/stockLevels.ts (both axes use 5).
        return 5m;
    }

    public static List<ProductVariant> ActiveVariants(Product p) =>
        p.ProductVariant?.Where(v => !v.IsDeleted).ToList() ?? new List<ProductVariant>();

    public static bool IsVariationManagement(Product p) => ManagementTypeNorm(p) == "variation";

    /// <summary>ערכים קנוניים ל-JSON (SPA): <c>quantity</c> | <c>status</c> | <c>variation</c>, או null אם לא מזוהה.</summary>
    public static string? StockManagementTypeForApi(Product p)
    {
        var n = ManagementTypeNorm(p);
        if (string.IsNullOrEmpty(n))
            return null;
        return n switch
        {
            "quantity" or "qty" => "quantity",
            "status" => "status",
            "variation" => "variation",
            _ => null,
        };
    }

    private static string ClassifyVariableUnitWeightStock(Product p)
    {
        var n = CountActiveWeightOptionTokens(p.WeightConfig?.WeightOptions);
        return n == 0 ? "out" : "ok";
    }

    private static string ClassifyStockVariation(Product p, Account? account, List<ProductVariant> variants)
    {
        var byQty = p.VariationStockByQuantity == true;
        if (byQty)
        {
            var threshold = ResolveLowThreshold(p, account);
            var lineStates = variants.Select(v => LineStateForQty(v.StockQuantity ?? 0m, threshold)).ToList();
            if (lineStates.Count == 0)
                return "out";

            var agg = AggregateLineStates(lineStates);
            var hasAnyLow = lineStates.Any(s => s == LineState.Low);
            if (agg == LineState.Out)
                return "out";
            if (agg == LineState.Low || (agg == LineState.Mixed && hasAnyLow))
                return "low";
            return "ok";
        }

        var binStates = variants.Select(v => (v.StockQuantity ?? 0m) > 0m ? LineState.Ok : LineState.Out).ToList();
        var bin = AggregateBinaryStates(binStates);
        if (bin == LineState.Out)
            return "out";
        if (bin == LineState.Mixed)
            return "low";
        return "ok";
    }

    private enum LineState
    {
        Ok,
        Low,
        Out,
        Mixed,
    }

    private static LineState LineStateForQty(decimal qty, decimal threshold)
    {
        if (qty <= 0m)
            return LineState.Out;
        if (threshold > 0m && qty <= threshold)
            return LineState.Low;
        return LineState.Ok;
    }

    private static LineState AggregateLineStates(List<LineState> states)
    {
        if (states.Count == 0)
            return LineState.Out;
        var first = states[0];
        return states.All(s => s == first) ? first : LineState.Mixed;
    }

    private static LineState AggregateBinaryStates(List<LineState> states)
    {
        if (states.Count == 0)
            return LineState.Out;
        var first = states[0];
        return states.All(s => s == first) ? first : LineState.Mixed;
    }

    private static string NormalizeStockStatusToken(Product p)
    {
        return (p.StockStatus?.Name ?? "")
            .Replace("_", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .ToLowerInvariant();
    }

    private static bool IsOnBackorderStatus(Product p)
    {
        var n = NormalizeStockStatusToken(p);
        return n.Contains("onbackorder", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOutOfStockStatus(Product p)
    {
        var n = NormalizeStockStatusToken(p);
        return n.Contains("outofstock", StringComparison.OrdinalIgnoreCase);
    }

    private static string ManagementTypeNorm(Product p) =>
        (p.StockManagementType?.Name ?? "").Trim().ToLowerInvariant();

    private static bool IsStatusOnlyManagement(Product p) => ManagementTypeNorm(p) == "status";

    private static bool IsExplicitQuantityManagement(Product p)
    {
        var n = ManagementTypeNorm(p);
        return n is "quantity" or "qty";
    }

    /// <summary>
    /// When false, catalog row has no meaningful on-hand number — classify from <see cref="IsOutOfStockStatus"/> only.
    /// </summary>
    public static bool UsesQuantityThresholds(Product p)
    {
        if (ManagementTypeNorm(p) == "status")
            return false;
        if (IsExplicitQuantityManagement(p))
            return true;
        return p.StockQuantity.HasValue;
    }

    /// <summary>
    /// My Products / inventory report: show numeric on-hand (not in/out only).
    /// <c>quantity</c>, or <c>variation</c> with <see cref="Product.VariationStockByQuantity"/> = true.
    /// </summary>
    public static bool UsesNumericStockDisplay(Product p)
    {
        var apiType = StockManagementTypeForApi(p);
        if (apiType == "status")
            return false;
        if (apiType == "quantity")
            return true;
        if (apiType == "variation")
        {
            var variants = ActiveVariants(p);
            return variants.Count > 0 && p.VariationStockByQuantity == true;
        }

        if (IsVariableUnitWeightProduct(p))
            return false;

        return UsesQuantityThresholds(p);
    }

    private static bool IsVariableUnitWeightProduct(Product p)
    {
        var setup = (p.SetupType?.Name ?? "").Trim().ToLowerInvariant();
        if (setup != "by_unit")
            return false;
        var mode = (p.WeightConfig?.UnitWeightMode?.Name ?? "").Trim().ToLowerInvariant();
        return mode == "variable";
    }

    private static int CountActiveWeightOptionTokens(string? weightOptionsRaw)
    {
        const string inactiveMarker = "##";
        var s = (weightOptionsRaw ?? "").Trim();
        if (string.IsNullOrEmpty(s))
            return 0;
        var idx = s.IndexOf(inactiveMarker, StringComparison.Ordinal);
        var activePart = idx < 0 ? s : s[..idx].Trim();
        if (string.IsNullOrEmpty(activePart))
            return 0;
        return activePart
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Count(x => x.Length > 0);
    }

    /// <summary>Same rule as SPA <c>stockLevels</c> / products report — not <see cref="Product.IsWeighted"/> alone.</summary>
    public static bool IsWeightedLikeProduct(Product p)
    {
        if (p.IsWeighted == true)
            return true;
        var setup = (p.SetupType?.Name ?? "").Trim().ToLowerInvariant();
        return setup is "by_weight" or "by_unit" or "by_unit_and_weight";
    }
}
