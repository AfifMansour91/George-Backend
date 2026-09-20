using George.Data;
using George.DB;
using George.Services.Request;

namespace George.Services.Bundles;

/// <summary>One resolved bundle slot, ready to become a child order line (ids already mapped to George ids).</summary>
public sealed class BundleLineSlot
{
    /// <summary>Definition slot (<c>ProductBundleComponent.Id</c>); null for bundles George does not know.</summary>
    public int? ComponentId { get; set; }
    /// <summary>0-based slot index (Woo <c>_oc_bundle_components</c> index / definition SortOrder position).</summary>
    public int SlotIndex { get; set; }
    /// <summary>Product actually in the slot (George id); null when the Woo product is not mapped on this site.</summary>
    public int? ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    /// <summary>Loaded catalog product (for display fields); may be null.</summary>
    public Product? Product { get; set; }
    public string? Title { get; set; }
    public string? VariantTitle { get; set; }
    /// <summary>Quantity per bundle in the component's own unit (kg for weight slots, pieces otherwise).</summary>
    public decimal QtyPerBundle { get; set; }
    /// <summary>True when the slot is measured in kg (weight line).</summary>
    public bool IsWeight { get; set; }
    /// <summary>Explicit total line quantity (overrides <see cref="QtyPerBundle"/> × bundles) when &gt; 0.</summary>
    public decimal? LineQuantityOverride { get; set; }
    /// <summary>Price of ONE SLOT UNIT of the product in the slot (per kg for kg slots; per piece otherwise - a product priced per kg but sold by units costs price × unit weight, like a regular order line) - informational <c>PricePerUnit</c>.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Weight (kg) chosen for one unit of the slot - only meaningful for "choose a weight" products.</summary>
    public decimal? UnitWeightKg { get; set; }
    /// <summary>Configured original product when the slot is swapped.</summary>
    public int? SwappedFromProductId { get; set; }
    /// <summary>Surcharge per bundle of the active swap (0 when the slot holds its configured product).</summary>
    public decimal Surcharge { get; set; }
    /// <summary>Already weighed total (e.g. Woo <c>actualQty</c>); stored as <c>PickedQuantity</c> with <c>PickingUserConfirmed = false</c>.</summary>
    public decimal? PickedQuantity { get; set; }
    public int? WooProductId { get; set; }
    public int? WooVariationId { get; set; }
    public string? Sku { get; set; }
}

/// <summary>Result of expanding one bundle order line: the parent and its children (children reference the parent by navigation).</summary>
public sealed class BundleLineExpansion
{
    public OrderItem Parent { get; init; } = null!;
    public List<OrderItem> Children { get; init; } = new();
    public IEnumerable<OrderItem> All => new[] { Parent }.Concat(Children);
}

/// <summary>
/// Pure bundle order-line logic shared by manual orders, Woo intake, picking and swaps
/// (BUNDLES_SYNC_SPEC.md §2 order-line semantics, §3.3, §4). No I/O: callers resolve ids / prices first.
/// </summary>
public static class BundleOrderLineBuilder
{
    public const string ErrSlotUnknown = "רכיב המארז אינו מוכר";
    public const string ErrFreeSwapNotAllowed = "החלפה חופשית של רכיבי מארז אינה מופעלת לאתר זה - ניתן לבחור רק מוצר מרשימת ההחלפות של הרכיב";
    public const string ErrChildNotSwappable = "ניתן להחליף רק רכיב של מארז";
    public const string ErrSlotNotSwappable = "הרכיב לא סומן כניתן להחלפה בהגדרת המארז";
    public const string ErrChildDelete = "לא ניתן להסיר רכיב בודד מהמארז - החלף את הרכיב או הסר את המארז כולו";
    public const string ErrNoDefinition = "למארז אין הגדרת רכיבים - יש להגדיר את רכיבי המארז לפני הזמנה";
    public const string ErrProductOtherAccount = "המוצר שנבחר לרכיב אינו שייך לחשבון של ההזמנה";
    public const string ErrProductNotOnSite = "המוצר שנבחר לרכיב אינו משויך לסניף של ההזמנה";

    /// <summary>Slots ordered as the definition orders them (SortOrder, then Id) - the slot index space.</summary>
    public static List<ProductBundleComponent> OrderedSlots(BundleDefinition definition) =>
        definition.Components.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();

    /// <summary>Finds the definition slot for a Woo component: by stable key first, then by index.</summary>
    public static ProductBundleComponent? MatchSlot(IReadOnlyList<ProductBundleComponent> orderedSlots, string? key, int? index)
    {
        var k = key?.Trim();
        if (!string.IsNullOrEmpty(k))
        {
            var byKey = orderedSlots.FirstOrDefault(c =>
                string.Equals(string.IsNullOrEmpty(c.ComponentKey) ? "c" + c.Id : c.ComponentKey, k, StringComparison.Ordinal));
            if (byKey != null) return byKey;
        }
        if (index is >= 0 && index.Value < orderedSlots.Count)
            return orderedSlots[index.Value];
        return null;
    }

    /// <summary>Quantity per bundle in the line unit: <c>grams</c> → kg; <c>kg</c>/<c>unit</c> as-is.</summary>
    public static (decimal Qty, bool IsWeight) NormalizeWooComponentQty(decimal? qty, string? unit, string? mode)
    {
        var q = qty ?? 0m;
        var u = (unit ?? "").Trim().ToLowerInvariant();
        var m = (mode ?? "").Trim().ToLowerInvariant();
        if (u == "grams" || u == "gram" || u == "g")
            return (BundlePricingEngine.Round4(q / 1000m), true);
        // `unit` is what the quantity is actually expressed in (George always sends it explicitly: "kg" for
        // by_weight slots, "unit" otherwise), so it wins. `mode` only decides when no unit was sent - a product
        // sold by units WITH a weight ("units_weight"/"weight" mode, unit "unit") must stay a units line.
        if (u == "kg" || (u.Length == 0 && m == "weight"))
            return (q, true);
        return (q, false);
    }

    /// <summary>
    /// A slot measured in kg. The catalog product decides first (a by_weight product is always sold in kg,
    /// and that is what George sends to the store); the Woo mirror is only consulted when the product is
    /// unknown. `mode` (derived by the plugin from the product) outranks the mirrored `unit`.
    /// </summary>
    public static bool IsWeightSlot(ProductBundleComponent slot, Product? product)
    {
        if (product != null)
        {
            var setupType = product.SetupType?.Name;
            if (!string.IsNullOrEmpty(setupType))
                return string.Equals(setupType, "by_weight", StringComparison.OrdinalIgnoreCase);
        }
        var mode = (slot.Mode ?? "").Trim().ToLowerInvariant();
        if (mode == "weight") return true;
        if (mode.Length > 0) return false;
        var unit = (slot.Unit ?? "").Trim().ToLowerInvariant();
        return unit == "kg" || unit == "grams";
    }

    /// <summary>
    /// Validates the product chosen for a slot and resolves its surcharge: the configured product (surcharge 0),
    /// one of the configured swaps (its surcharge), or - only with <paramref name="allowFreeSwap"/> - any product
    /// (<paramref name="requestedSurcharge"/>). A slot not marked <c>Swappable</c> accepts only its configured
    /// product, free swap or not. Returns a Hebrew error, or null when valid.
    /// <paramref name="resolvedVariantId"/> is the variant the slot ends up with: the configured product sent
    /// without a variant takes the slot's configured variant (it is the configured product, not a free swap).
    /// </summary>
    public static string? ResolveSlotSelection(
        ProductBundleComponent slot,
        int productId,
        int? productVariantId,
        bool allowFreeSwap,
        decimal? requestedSurcharge,
        out decimal surcharge,
        out bool isSwap,
        out bool isFreeSwap,
        out int? resolvedVariantId)
    {
        surcharge = 0m;
        isSwap = false;
        isFreeSwap = false;
        var variant = productVariantId is > 0 ? productVariantId : null;
        resolvedVariantId = variant;
        if (productId <= 0) return "חסר מזהה מוצר לרכיב המארז";

        if (productId == slot.ComponentProductId && (variant ?? 0) == (slot.ComponentVariantId ?? 0))
            return null;
        if (productId == slot.ComponentProductId && variant == null && slot.ComponentVariantId is > 0)
        {
            resolvedVariantId = slot.ComponentVariantId;
            return null;
        }

        // "ניתן להחלפה" is the manager's decision per slot; the site's free-swap flag only widens WHAT a swappable slot takes.
        if (!slot.Swappable)
            return ErrSlotNotSwappable;

        var configured = slot.Swaps
            .Where(s => !s.IsDeleted)
            .FirstOrDefault(s => s.SwapProductId == productId && (s.SwapVariantId ?? 0) == (variant ?? 0))
            ?? slot.Swaps.Where(s => !s.IsDeleted).FirstOrDefault(s => s.SwapProductId == productId && !s.SwapVariantId.HasValue);
        if (configured != null)
        {
            isSwap = true;
            surcharge = BundlePricingEngine.Round2(configured.Surcharge);
            return null;
        }

        if (!allowFreeSwap)
            return ErrFreeSwapNotAllowed;
        isSwap = true;
        isFreeSwap = true;
        surcharge = BundlePricingEngine.Round2(Math.Max(0m, requestedSurcharge ?? 0m));
        return null;
    }

    /// <summary>The configured alternative of a slot for a product/variant (exact variant first, then "any variant"), or null.</summary>
    public static ProductBundleComponentSwap? FindConfiguredSwap(ProductBundleComponent slot, int productId, int? productVariantId)
    {
        var variant = productVariantId is > 0 ? productVariantId : null;
        var live = slot.Swaps.Where(s => !s.IsDeleted).ToList();
        return live.FirstOrDefault(s => s.SwapProductId == productId && (s.SwapVariantId ?? 0) == (variant ?? 0))
            ?? live.FirstOrDefault(s => s.SwapProductId == productId && !s.SwapVariantId.HasValue);
    }

    /// <summary>
    /// Quantity of a product that replaces another in a slot, in the NEW product's unit. Same kind of unit → unchanged.
    /// Units → kg: units × the old unit weight; kg → units: kg ÷ the new unit weight, rounded to whole units (≥ 1).
    /// Without a usable unit weight the number is kept as-is.
    /// </summary>
    public static decimal ConvertSwapQuantity(decimal qty, bool fromWeight, decimal? fromUnitWeightKg, bool toWeight, decimal? toUnitWeightKg)
    {
        if (qty <= 0m) return qty;
        if (fromWeight == toWeight) return qty;
        if (fromWeight)
        {
            if (toUnitWeightKg is not > 0m) return Math.Max(1m, Math.Round(qty, 0, MidpointRounding.AwayFromZero));
            return Math.Max(1m, Math.Round(qty / toUnitWeightKg.Value, 0, MidpointRounding.AwayFromZero));
        }
        if (fromUnitWeightKg is not > 0m) return qty;
        return BundlePricingEngine.Round4(qty * fromUnitWeightKg.Value);
    }

    /// <summary>Inverse of <see cref="BundlePricingEngine.ApplyDiscount"/>: the raw price a base price came from (ratio only).</summary>
    public static decimal RawFromBase(decimal basePrice, string? discountType, decimal discountValue)
    {
        if (basePrice <= 0m) return 0m;
        if (string.Equals(discountType, BundlePricingEngine.DiscountPercent, StringComparison.OrdinalIgnoreCase) && discountValue > 0m)
            return discountValue >= 100m ? basePrice : basePrice / (1m - discountValue / 100m);
        if (string.Equals(discountType, BundlePricingEngine.DiscountFixed, StringComparison.OrdinalIgnoreCase) && discountValue > 0m)
            return basePrice + discountValue;
        return basePrice;
    }

    /// <summary>
    /// A child's picked quantity in the SLOT unit. The picking UI stores kg for a units line that carries a
    /// per-unit weight (200 g portions → 0.8 kg for 4 pieces, see <see cref="OrderItemStockConsumption.LinePickingStoresKg"/>);
    /// the slot counts pieces, so kg ÷ unit weight (fractional pieces = the weighed share). Otherwise as stored.
    /// </summary>
    public static decimal? PickedQuantityInSlotUnit(OrderItem child)
    {
        if (child.PickedQuantity is not > 0m) return child.PickedQuantity;
        if (string.Equals(child.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase)) return child.PickedQuantity;
        var unitKg = OrderItemStockConsumption.TryGetUnitWeightKg(child);
        return unitKg is > 0m ? BundlePricingEngine.Round4(child.PickedQuantity.Value / unitKg.Value) : child.PickedQuantity;
    }

    /// <summary>Inverse: a slot-unit quantity (Woo actualQty in pieces) as the picking UI stores it (kg for weighed-piece lines).</summary>
    public static decimal? SlotUnitQuantityToPicked(OrderItem child, decimal? slotQty)
    {
        if (slotQty is not > 0m) return slotQty;
        if (string.Equals(child.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase)) return slotQty;
        var unitKg = OrderItemStockConsumption.TryGetUnitWeightKg(child);
        return unitKg is > 0m ? BundlePricingEngine.Round4(slotQty.Value * unitKg.Value) : slotQty;
    }

    /// <summary>Base price per bundle implied by a parent line: unit price minus the children's surcharges (clamped ≥ 0).</summary>
    public static decimal BaseFromParent(OrderItem parent, IEnumerable<OrderItem> children)
    {
        var surcharges = children.Sum(c => c.SwapSurcharge ?? 0m);
        return Math.Max(0m, (parent.PricePerUnit ?? 0m) - surcharges);
    }

    /// <summary>
    /// Builds the parent + child lines of one bundle. The caller sets the parent's <c>ProductId</c>, <c>Title</c>,
    /// <c>PricePerUnit</c> and <c>TotalPrice</c> first (pricing engine for manual orders, payload for Woo).
    /// Children: quantity = slot qty × bundles (or the explicit override), kg lines carry <c>UnitWeightGrams = 1000</c>,
    /// <c>TotalPrice</c> = share in <c>sum</c> mode (informational) and null in <c>fixed</c> mode.
    /// <paramref name="basePrice"/> overrides the base derived from the parent line (Woo sends it).
    /// </summary>
    public static BundleLineExpansion Expand(
        OrderItem parent,
        decimal bundleQty,
        IReadOnlyList<BundleLineSlot> slots,
        string? pricingMode,
        string? discountType,
        decimal discountValue,
        int firstSortOrder,
        decimal? basePrice = null)
    {
        parent.BundleProductId = parent.ProductId;
        parent.ParentOrderItemId = null;
        parent.ParentOrderItem = null;
        parent.Quantity = bundleQty;
        parent.SortOrder = firstSortOrder;
        parent.OrderLineQuantityMode = "units";
        parent.UnitWeightGrams = null;
        parent.DepreciationPercent = null;
        // Not pickable: the picked quantity mirrors the ordered bundles; confirmation is derived from the children.
        parent.PickedQuantity = bundleQty;
        parent.PickingUserConfirmed = false;

        var children = new List<OrderItem>(slots.Count);
        var i = 0;
        foreach (var slot in slots.OrderBy(s => s.SlotIndex))
        {
            var lineQty = slot.LineQuantityOverride is > 0m
                ? slot.LineQuantityOverride.Value
                : BundlePricingEngine.Round4(slot.QtyPerBundle * bundleQty);
            var swapped = slot.SwappedFromProductId.HasValue && slot.SwappedFromProductId != slot.ProductId;
            var child = new OrderItem
            {
                OrderId = parent.OrderId,
                ProductId = slot.ProductId,
                ProductVariantId = slot.ProductVariantId,
                Title = slot.Title,
                VariantTitle = slot.VariantTitle,
                Quantity = lineQty,
                UnitWeightGrams = slot.IsWeight ? 1000m : null,
                OrderLineQuantityMode = slot.IsWeight ? "weight" : "units",
                PricePerUnit = slot.UnitPrice,
                TotalPrice = null,
                BundleComponentId = slot.ComponentId,
                BundleComponentIndex = slot.SlotIndex,
                SwappedFromProductId = swapped ? slot.SwappedFromProductId : null,
                SwapSurcharge = swapped || slot.Surcharge > 0m ? slot.Surcharge : null,
                PickedQuantity = slot.PickedQuantity is > 0m ? slot.PickedQuantity : null,
                PickingUserConfirmed = false,
                WooCommerceProductId = slot.WooProductId,
                WooCommerceVariationId = slot.WooVariationId,
                LineSku = string.IsNullOrWhiteSpace(slot.Sku) ? null : slot.Sku.Trim(),
                SortOrder = firstSortOrder + 1 + i,
                ParentOrderItem = parent,
                IsDeleted = false,
            };
            if (slot.ProductId is > 0)
            {
                OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(child, new CreateOrderItemReq
                {
                    ProductId = slot.ProductId,
                    ProductVariantId = slot.ProductVariantId,
                    Quantity = lineQty,
                    UnitWeightGrams = slot.IsWeight ? 1000m : null,
                }, slot.Product);
                // The slot's unit is fixed by the bundle definition - never let the catalog heuristics flip it.
                child.OrderLineQuantityMode = slot.IsWeight ? "weight" : "units";
                if (slot.IsWeight) child.UnitWeightGrams = 1000m;
                // Woo's actualQty arrives in the slot unit (pieces); the picking UI keeps kg for weighed-piece lines.
                child.PickedQuantity = SlotUnitQuantityToPicked(child, child.PickedQuantity);
            }
            children.Add(child);
            i++;
        }

        ApplyChildShares(parent, children, pricingMode, discountType, discountValue, basePrice, useOrderedQuantities: true);
        return new BundleLineExpansion { Parent = parent, Children = children };
    }

    /// <summary>
    /// Sum mode: writes each child's informational share (<c>price × qty × base/raw + surcharge × bundles</c>) and returns
    /// the Σ of shares; fixed mode: clears child totals and returns null. Never touches the parent total.
    /// </summary>
    public static decimal? ApplyChildShares(
        OrderItem parent,
        IReadOnlyList<OrderItem> children,
        string? pricingMode,
        string? discountType,
        decimal discountValue,
        decimal? basePrice,
        bool useOrderedQuantities)
    {
        if (!BundlePricingEngine.IsSumMode(pricingMode))
        {
            foreach (var c in children) c.TotalPrice = null;
            return null;
        }
        var basePer = basePrice ?? BaseFromParent(parent, children);
        var raw = RawFromBase(basePer, discountType, discountValue);

        if (useOrderedQuantities)
        {
            // Ordered state: the parent's money is what the customer pays (engine / store); the children only
            // SHOW how it splits. Split the base (parent minus surcharges) by the catalog value of what is in each
            // slot and add each slot's surcharge, so the shares always add up to the parent - also after a swap,
            // where price × qty of the swapped-in product no longer equals the original's (spec §4: the base is
            // built from the ORIGINAL components, a swap only adds its surcharge). Same rule as the plugin's
            // invoice split of an externally priced line.
            var bundles = Math.Max(0m, parent.Quantity);
            var parentTotal = parent.TotalPrice ?? BundlePricingEngine.Round2((parent.PricePerUnit ?? 0m) * bundles);
            var surchargeTotal = children.Sum(c => (c.SwapSurcharge ?? 0m) * bundles);
            var baseTotal = Math.Max(0m, parentTotal - surchargeTotal);
            var rawValues = children.Select(c => Math.Max(0m, (c.PricePerUnit ?? 0m) * c.Quantity)).ToList();
            var rawSum = rawValues.Sum();
            var assigned = 0m;
            for (var i = 0; i < children.Count; i++)
            {
                var share = rawSum > 0m ? baseTotal * rawValues[i] / rawSum : 0m;
                share += (children[i].SwapSurcharge ?? 0m) * bundles;
                share = BundlePricingEngine.Round2(share);
                // Rounding remainder lands on the last child so Σ shares == parent total exactly.
                if (i == children.Count - 1) share = BundlePricingEngine.Round2(parentTotal - assigned);
                children[i].TotalPrice = Math.Max(0m, share);
                assigned += children[i].TotalPrice!.Value;
            }
            return parentTotal;
        }

        var picked = new List<BundleReweighComponent>(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            var c = children[i];
            // Re-weigh prices the slot unit (price per piece × pieces, price per kg × kg): a weighed-piece line stores kg.
            var qty = !useOrderedQuantities && c.PickingUserConfirmed && c.PickedQuantity.HasValue
                ? PickedQuantityInSlotUnit(c) ?? c.Quantity
                : c.Quantity;
            picked.Add(new BundleReweighComponent
            {
                ComponentId = i,
                UnitPrice = c.PricePerUnit ?? 0m,
                PickedQty = qty,
                Surcharge = c.SwapSurcharge ?? 0m,
            });
        }
        // The ratio comes from THIS ORDER, not from the bundle config: what the customer paid for the contents
        // (parent unit price minus surcharges - already after the bundle discount AND any coupon / promotion the
        // store applied) over the catalog value of what was ordered. So weighing exactly the ordered quantities
        // gives exactly the ordered total, a coupon survives the re-weigh, and a swapped-in dearer product does
        // not re-price the bundle by itself.
        var bundleCount = Math.Max(0m, parent.Quantity);
        var paidBaseTotal = Math.Max(0m, (parent.PricePerUnit ?? 0m) * bundleCount - children.Sum(c => (c.SwapSurcharge ?? 0m) * bundleCount));
        var orderedCatalogTotal = children.Sum(c => Math.Max(0m, (c.PricePerUnit ?? 0m) * c.Quantity));
        var result = orderedCatalogTotal > 0m
            ? BundlePricingEngine.Reweigh(paidBaseTotal, orderedCatalogTotal, parent.Quantity, picked)
            : BundlePricingEngine.Reweigh(basePer, raw, parent.Quantity, picked);
        for (var i = 0; i < children.Count; i++)
            children[i].TotalPrice = result.Shares.TryGetValue(i, out var share) ? share : null;
        return result.ParentTotal;
    }

    /// <summary>
    /// After a picking save (spec §3.3 / §4): the parent is confirmed when every child is; its picked quantity mirrors
    /// the ordered bundles; פחת is never stamped on bundle lines; in <c>sum</c> + <c>ReweighPrice</c> the parent total
    /// follows the weighed components (<see cref="BundlePricingEngine.Reweigh"/>), otherwise it is left as ordered.
    /// The re-weigh is skipped (parent keeps its ordered total) when any child has no usable <c>PricePerUnit</c> -
    /// a share of 0 for such a child would silently under-charge the bundle.
    /// Returns true when the parent's money changed.
    /// </summary>
    public static bool ApplyPickingRules(
        OrderItem parent,
        IReadOnlyList<OrderItem> children,
        string? pricingMode,
        bool reweighPrice,
        string? discountType,
        decimal discountValue)
    {
        parent.PickingUserConfirmed = children.Count > 0 && children.All(c => c.PickingUserConfirmed);
        parent.PickedQuantity = parent.Quantity;
        parent.DepreciationPercent = null;
        foreach (var c in children) c.DepreciationPercent = null;

        var before = parent.TotalPrice;
        var sumMode = BundlePricingEngine.IsSumMode(pricingMode);
        var anyWeighed = children.Any(c => c.PickingUserConfirmed && c.PickedQuantity is > 0m);
        if (sumMode && reweighPrice && anyWeighed && !children.Any(c => c.PricePerUnit is null or <= 0m))
        {
            var total = ApplyChildShares(parent, children, pricingMode, discountType, discountValue, null, useOrderedQuantities: false);
            if (total.HasValue) parent.TotalPrice = total.Value;
        }
        else if (sumMode && !children.Any(c => c.PricePerUnit is null or <= 0m))
        {
            // Re-weighed bundle with nothing weighed (any more): back to the ordered price - the unit price is the
            // ordered money (a swap moves it by the surcharge delta only), the total is what re-weighing changes.
            if (reweighPrice && !anyWeighed && parent.PricePerUnit is > 0m)
                parent.TotalPrice = BundlePricingEngine.Round2(parent.PricePerUnit.Value * Math.Max(0m, parent.Quantity));
            // Nothing weighed yet (or no re-weigh): the parent keeps its money; re-split it over the children
            // (a swap may have changed what is in a slot).
            ApplyChildShares(parent, children, pricingMode, discountType, discountValue, null, useOrderedQuantities: true);
        }
        else if (!sumMode)
        {
            foreach (var c in children) c.TotalPrice = null;
        }
        // sum mode with a price-less child: parent and shares are left as they are (a 0 share would mislead).
        return before != parent.TotalPrice;
    }

    /// <summary>Slot deviations (product / variant / surcharge) of a parent's children, keyed by slot id - the pricing engine's swap map.</summary>
    public static Dictionary<int, BundleService.BundleSlotSwap> SwapsFromChildren(IEnumerable<OrderItem> children)
    {
        var map = new Dictionary<int, BundleService.BundleSlotSwap>();
        foreach (var c in children)
        {
            if (c.BundleComponentId is not > 0 || c.ProductId is not > 0) continue;
            if (!c.SwappedFromProductId.HasValue && (c.SwapSurcharge ?? 0m) <= 0m) continue;
            map[c.BundleComponentId.Value] = new BundleService.BundleSlotSwap
            {
                ProductId = c.ProductId.Value,
                VariantId = c.ProductVariantId,
                Surcharge = c.SwapSurcharge ?? 0m,
            };
        }
        return map;
    }
}
