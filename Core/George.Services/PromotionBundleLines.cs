using George.DB;

namespace George.Services;

/// <summary>
/// Bundle (מארז) rules for promotion evaluation on order lines (BUNDLES_SYNC_SPEC.md §9):
/// a bundle parent takes part as ONE product (its own product id / categories, count = number of
/// bundles), child (component) lines are never evaluated, and a Woo-sourced order never gains a
/// George promotion stamp on a bundle parent that Woo did not stamp itself.
/// </summary>
public static class PromotionBundleLines
{
    /// <summary>True when the line may enter the promotion evaluator (plain line or bundle parent).</summary>
    public static bool IsEvaluable(OrderItem? line) =>
        line != null && !BundleOrderLines.IsBundleChild(line);

    /// <summary>Order lines that the evaluator may see - bundle children removed.</summary>
    public static List<OrderItem> FilterEvaluable(IEnumerable<OrderItem> lines) =>
        lines.Where(IsEvaluable).ToList();

    /// <summary>A bundle whose config discounts the components' sum counts as a discounted product.</summary>
    public static bool IsDiscountedBundle(ProductBundleConfig? config) =>
        config != null &&
        !string.Equals((config.DiscountType ?? "none").Trim(), "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>Orders that came from the WooCommerce storefront (promotions were stamped by Woo on ingest).</summary>
    public static bool IsWooSourcedOrder(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return false;
        var s = source.Trim().ToLowerInvariant();
        return s == "woocommerce" || s == "website" || s == "web";
    }

    /// <summary>Promotion stamp of a line before a re-evaluation (so it can be restored).</summary>
    public readonly record struct StampSnapshot(int? PromotionId, decimal? DiscountAmount);

    /// <summary>Snapshot the promotion stamps of every bundle parent line, keyed by line id (or reference when unsaved).</summary>
    public static Dictionary<OrderItem, StampSnapshot> SnapshotBundleParentStamps(IEnumerable<OrderItem> lines)
    {
        var map = new Dictionary<OrderItem, StampSnapshot>(ReferenceEqualityComparer.Instance);
        foreach (var line in lines)
        {
            if (BundleOrderLines.IsBundleParent(line))
                map[line] = new StampSnapshot(line.PromotionId, line.DiscountAmount);
        }
        return map;
    }

    /// <summary>
    /// After a picking re-evaluation of a Woo-sourced order: a bundle parent that Woo had NOT stamped
    /// (no George-linked promotion before) must not come out with a new promotion - its stamp is put
    /// back to what it was. Parents that were stamped keep whatever the evaluator re-derived (re-scaled).
    /// Returns the number of parents that were reverted.
    /// </summary>
    public static int RevertNewStampsOnUnstampedBundleParents(
        IEnumerable<OrderItem> lines,
        IReadOnlyDictionary<OrderItem, StampSnapshot> before)
    {
        var reverted = 0;
        foreach (var line in lines)
        {
            if (!BundleOrderLines.IsBundleParent(line)) continue;
            if (!before.TryGetValue(line, out var snap)) continue;
            if (snap.PromotionId is > 0) continue;          // Woo stamped it - re-scaling is allowed
            if (line.PromotionId is not > 0) continue;      // evaluator did not add anything
            line.PromotionId = snap.PromotionId;
            line.DiscountAmount = snap.DiscountAmount;
            reverted++;
        }
        return reverted;
    }
}
