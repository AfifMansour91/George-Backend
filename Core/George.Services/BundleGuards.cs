using George.Services.Request;

namespace George.Services;

/// <summary>
/// Bundles (מארזים) catalog guards shared by ProductService / WooCommerceService: the request invariants a
/// bundle product must keep, the Hebrew errors for the delete / bulk-import guards, and the Woo sync ordering
/// that pushes component products before the bundles that reference them. Pure logic (unit-tested).
/// </summary>
public static class BundleGuards
{
    /// <summary>How many bundle names the delete-guard message lists before truncating.</summary>
    public const int MaxNamesInDeleteMessage = 3;

    /// <summary>
    /// A request that creates/turns a product into a bundle carries no variants/options, no price of its own
    /// (the price is computed from the definition) and is stock-managed by status, always in stock in George
    /// (availability comes from Woo / OC Bundles). Applied unconditionally so bulk edits / quick edits cannot
    /// write a fake price or an out-of-stock flag onto a bundle. Spec §1.
    /// </summary>
    public static void ApplyBundleRequestInvariants(ProductReq req, bool existingHasVariants)
    {
        req.Price = null;
        req.SalePrice = null;
        req.SalePriceStartDate = null;
        req.SalePriceEndDate = null;
        req.StockManagementType = "status";
        req.StockStatus = "in_stock";
        req.StockQuantity = null;
        req.VariationStockByQuantity = null;
        // Drop any variants/options: a bundle never has them (an update that converts a variable product
        // sends empty lists so the existing variants are soft-deleted; a create simply sends none).
        req.ProductOptions = existingHasVariants ? new List<ProductOptionReq>() : null;
        req.Variants = existingHasVariants ? new List<ProductVariantReq>() : null;
    }

    /// <summary>"לא ניתן למחוק - המוצר משמש כרכיב במארזים: X, Y" (up to 3 names, "ועוד N" for the rest).</summary>
    public static string ComponentInUseMessage(IReadOnlyList<string> bundleNames)
    {
        var shown = bundleNames.Take(MaxNamesInDeleteMessage).ToList();
        var msg = "לא ניתן למחוק - המוצר משמש כרכיב במארזים: " + string.Join(", ", shown);
        var rest = bundleNames.Count - shown.Count;
        if (rest > 0) msg += $" ועוד {rest}";
        return msg;
    }

    /// <summary>Bulk import row that matched an existing bundle product: it is never updated from a CSV.</summary>
    public static string BulkImportExistingBundleError(string? productName) =>
        $"המוצר {DisplayName(productName)} הוא מארז - יש לערוך אותו במסך המארזים";

    /// <summary>Bulk import row that would create (or convert to) a product with setup type 'bundle'.</summary>
    public static string BulkImportCreateBundleError(string? productName) =>
        $"המוצר {DisplayName(productName)} הוא מארז - יש ליצור אותו במסך המארזים";

    private static string DisplayName(string? name) => string.IsNullOrWhiteSpace(name) ? "(ללא שם)" : name.Trim();

    /// <summary>
    /// Woo sync order: every non-bundle product first (parallel batches of <paramref name="batchSize"/>, original
    /// order kept), then each bundle in its own batch (sequential) so the component Woo ids exist before the
    /// bundle PUT resolves them. Ids are de-duplicated; ids in <paramref name="bundleIds"/> that are not in
    /// <paramref name="ids"/> are ignored.
    /// </summary>
    public static List<List<int>> PartitionSyncBatches(IEnumerable<int> ids, IReadOnlySet<int> bundleIds, int batchSize)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var distinct = ids.Distinct().ToList();
        var regular = distinct.Where(id => !bundleIds.Contains(id)).ToList();
        var bundles = distinct.Where(bundleIds.Contains).ToList();

        var batches = new List<List<int>>();
        for (var i = 0; i < regular.Count; i += batchSize)
            batches.Add(regular.Skip(i).Take(batchSize).ToList());
        foreach (var b in bundles)
            batches.Add(new List<int> { b });
        return batches;
    }
}
