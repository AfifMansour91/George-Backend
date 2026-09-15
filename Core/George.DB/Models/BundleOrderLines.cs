namespace George.DB;

/// <summary>
/// Bundle (מארז) order-line classification. Use these everywhere instead of ad-hoc checks
/// (BUNDLES_SYNC_SPEC.md §2 "order-line semantics"):
/// parent line = the bundle itself (money, totals, promotions, after-picking prints);
/// child lines = its components (quantity report, stock consumption, component demand).
/// Lives in George.DB so storage (George.Data) and services share one definition.
/// </summary>
public static class BundleOrderLines
{
    /// <summary>The bundle line: <c>BundleProductId</c> set and not itself a child.</summary>
    public static bool IsBundleParent(OrderItem? line) =>
        line != null && line.BundleProductId.HasValue && !IsBundleChild(line);

    /// <summary>
    /// A component line of a bundle: <c>ParentOrderItemId</c> set, or (before the first save, when the id is
    /// not assigned yet) the <c>ParentOrderItem</c> navigation set.
    /// </summary>
    public static bool IsBundleChild(OrderItem? line) =>
        line != null && (line.ParentOrderItemId.HasValue || line.ParentOrderItem != null);

    /// <summary>A regular (non-bundle) line.</summary>
    public static bool IsPlainLine(OrderItem? line) =>
        line != null && !IsBundleParent(line) && !IsBundleChild(line);

    /// <summary>Lines that carry money / count as order lines: plain lines and bundle parents (children removed).</summary>
    public static IEnumerable<OrderItem> WithoutChildren(IEnumerable<OrderItem> lines) =>
        lines.Where(l => !IsBundleChild(l));

    /// <summary>The (non-deleted) child lines of a saved parent, in sort order.</summary>
    public static List<OrderItem> ChildrenOf(IEnumerable<OrderItem> lines, OrderItem parent) =>
        lines.Where(l => !l.IsDeleted && IsChildOf(l, parent)).OrderBy(l => l.SortOrder).ThenBy(l => l.Id).ToList();

    /// <summary>True when <paramref name="line"/> is a child of <paramref name="parent"/> (by id when saved, by reference before).</summary>
    public static bool IsChildOf(OrderItem line, OrderItem parent) =>
        (parent.Id > 0 && line.ParentOrderItemId == parent.Id) || ReferenceEquals(line.ParentOrderItem, parent);
}
