namespace George.Services.Bundles;

/// <summary>One configured component slot as the pricing engine sees it (prices already resolved per site).</summary>
public sealed class BundlePricingComponent
{
    public int ComponentId { get; set; }
    /// <summary>Product actually in the slot (configured component, or the swap when swapped).</summary>
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    /// <summary>Quantity per bundle in the component's own unit (kg or units).</summary>
    public decimal Qty { get; set; }
    /// <summary>Price of the ORIGINAL configured component product/variant (per kg / per unit). Drives <c>raw</c>.</summary>
    public decimal OriginalUnitPrice { get; set; }
    /// <summary>Surcharge per bundle of the active swap (0 when the slot holds its configured product).</summary>
    public decimal Surcharge { get; set; }
}

public sealed class BundlePricingInput
{
    /// <summary>fixed | sum</summary>
    public string PricingMode { get; set; } = "fixed";
    public decimal? FixedPrice { get; set; }
    /// <summary>none | percent | fixed</summary>
    public string DiscountType { get; set; } = "none";
    public decimal DiscountValue { get; set; }
    public decimal BundleQty { get; set; } = 1m;
    public List<BundlePricingComponent> Components { get; set; } = new();
}

public sealed class BundlePricedComponent
{
    public int ComponentId { get; set; }
    public int ProductId { get; set; }
    public int? ProductVariantId { get; set; }
    /// <summary>Per bundle.</summary>
    public decimal Qty { get; set; }
    /// <summary>Qty × bundles - the child order line quantity.</summary>
    public decimal LineQty { get; set; }
    public decimal Surcharge { get; set; }
    /// <summary>Informational share of the bundle money (sum mode only); null in fixed mode.</summary>
    public decimal? Share { get; set; }
}

public sealed class BundlePricingResult
{
    /// <summary>Per bundle: base + surcharges.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Per bundle after the bundle discount, before surcharges.</summary>
    public decimal BasePrice { get; set; }
    /// <summary>Per bundle before the discount (fixed price, or Σ component catalog prices).</summary>
    public decimal RawPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SurchargeTotal { get; set; }
    /// <summary>UnitPrice × bundle quantity.</summary>
    public decimal LineTotal { get; set; }
    public List<BundlePricedComponent> Components { get; set; } = new();
}

/// <summary>One picked (weighed) component for the re-weigh share computation.</summary>
public sealed class BundleReweighComponent
{
    public int ComponentId { get; set; }
    /// <summary>Catalog price (per kg / per unit) of the product actually in the slot.</summary>
    public decimal UnitPrice { get; set; }
    /// <summary>Picked total for the child line (all bundles of the order line).</summary>
    public decimal PickedQty { get; set; }
    /// <summary>Surcharge per bundle of the active swap.</summary>
    public decimal Surcharge { get; set; }
}

public sealed class BundleReweighResult
{
    /// <summary>base / raw (1 when raw = 0).</summary>
    public decimal Ratio { get; set; }
    /// <summary>New parent TotalPrice = Σ shares.</summary>
    public decimal ParentTotal { get; set; }
    /// <summary>componentId → share (child TotalPrice, informational).</summary>
    public Dictionary<int, decimal> Shares { get; set; } = new();
}

/// <summary>
/// George-side bundle pricing. Mirrors oc-bundle <c>class-oc-bundles-pricing.php</c> exactly:
/// <code>
/// raw       = fixed ? FixedPrice : Σ over ORIGINAL config components of price × qty   (clamp ≥ 0, round 2)
/// base      = discount none → raw ; percent → raw × (1 − v/100) ; fixed → raw − v ; clamp ≥ 0 ; round 2
/// unitPrice = base + Σ surcharge of active swaps                                        (clamp ≥ 0, round 2)
/// lineTotal = unitPrice × bundleQty
/// </code>
/// Spec: BUNDLES_SYNC_SPEC.md §4. Depreciation (פחת) is never applied to bundle lines.
/// </summary>
public static class BundlePricingEngine
{
    public const string PricingModeFixed = "fixed";
    public const string PricingModeSum = "sum";
    public const string DiscountNone = "none";
    public const string DiscountPercent = "percent";
    public const string DiscountFixed = "fixed";

    public static bool IsSumMode(string? pricingMode) =>
        string.Equals(pricingMode, PricingModeSum, StringComparison.OrdinalIgnoreCase);

    /// <summary>raw: the fixed price, or Σ original component price × qty. Clamped ≥ 0, rounded to 2 decimals.</summary>
    public static decimal RawPrice(BundlePricingInput input)
    {
        decimal raw;
        if (IsSumMode(input.PricingMode))
        {
            raw = 0m;
            foreach (var c in input.Components)
                raw += c.OriginalUnitPrice * c.Qty;
        }
        else
        {
            raw = input.FixedPrice ?? 0m;
        }
        return Clamp2(raw);
    }

    /// <summary>The bundle-level discount applied to a price (percent / fixed / none), clamped ≥ 0. Not rounded.</summary>
    public static decimal ApplyDiscount(decimal price, string? discountType, decimal discountValue)
    {
        if (string.Equals(discountType, DiscountPercent, StringComparison.OrdinalIgnoreCase) && discountValue > 0m)
            price -= price * (discountValue / 100m);
        else if (string.Equals(discountType, DiscountFixed, StringComparison.OrdinalIgnoreCase) && discountValue > 0m)
            price -= discountValue;
        return Math.Max(0m, price);
    }

    /// <summary>base: raw after the bundle discount. Clamped ≥ 0, rounded to 2 decimals.</summary>
    public static decimal BasePrice(BundlePricingInput input)
    {
        var raw = RawPrice(input);
        return Clamp2(ApplyDiscount(raw, input.DiscountType, input.DiscountValue));
    }

    /// <summary>Full pricing of a bundle line: unit price, base, raw, discount, surcharges and line total.</summary>
    public static BundlePricingResult Price(BundlePricingInput input)
    {
        var raw = RawPrice(input);
        var basePrice = Clamp2(ApplyDiscount(raw, input.DiscountType, input.DiscountValue));
        var discount = Clamp2(raw - ApplyDiscount(raw, input.DiscountType, input.DiscountValue));
        var surchargeTotal = 0m;
        foreach (var c in input.Components)
            surchargeTotal += c.Surcharge;
        surchargeTotal = Round2(surchargeTotal);

        var unitPrice = Clamp2(basePrice + surchargeTotal);
        var bundleQty = input.BundleQty <= 0m ? 0m : input.BundleQty;
        var lineTotal = Round2(unitPrice * bundleQty);

        var sumMode = IsSumMode(input.PricingMode);
        var ratio = raw > 0m ? basePrice / raw : 1m;

        var result = new BundlePricingResult
        {
            UnitPrice = unitPrice,
            BasePrice = basePrice,
            RawPrice = raw,
            DiscountAmount = discount,
            SurchargeTotal = surchargeTotal,
            LineTotal = lineTotal,
        };
        foreach (var c in input.Components)
        {
            result.Components.Add(new BundlePricedComponent
            {
                ComponentId = c.ComponentId,
                ProductId = c.ProductId,
                ProductVariantId = c.ProductVariantId,
                Qty = c.Qty,
                LineQty = Round4(c.Qty * bundleQty),
                Surcharge = c.Surcharge,
                // Informational catalog share before any weighing: original price × qty × ratio + surcharge, all bundles.
                Share = sumMode
                    ? Round2((c.OriginalUnitPrice * c.Qty * bundleQty) * ratio + c.Surcharge * bundleQty)
                    : null,
            });
        }
        return result;
    }

    /// <summary>
    /// Re-weigh (sum mode AND ReweighPrice): <c>ratio = base / raw</c> (1 when raw = 0);
    /// <c>share_i = price_i × pickedQty_i × ratio + surcharge_i × bundleQty</c>; parent total = Σ share_i.
    /// The caller decides whether re-weigh applies (fixed mode never changes the parent on picking).
    /// </summary>
    public static BundleReweighResult Reweigh(decimal basePrice, decimal rawPrice, decimal bundleQty, IEnumerable<BundleReweighComponent> picked)
    {
        var ratio = rawPrice > 0m ? basePrice / rawPrice : 1m;
        var qty = bundleQty <= 0m ? 0m : bundleQty;
        var result = new BundleReweighResult { Ratio = ratio };
        var total = 0m;
        foreach (var c in picked)
        {
            var share = Round2(c.UnitPrice * c.PickedQty * ratio + c.Surcharge * qty);
            result.Shares[c.ComponentId] = share;
            total += share;
        }
        result.ParentTotal = Clamp2(total);
        return result;
    }

    /// <summary>
    /// The price the shop charges today for a product: site override (price / sale in window) → product
    /// sale price (in window) → product price. Weight products: per kg. Spec §4 "price(...)".
    /// </summary>
    public static decimal ResolveProductPrice(
        decimal? price, decimal? salePrice, DateTime? saleFrom, DateTime? saleTo,
        decimal? overridePrice, decimal? overrideSalePrice, DateTime? overrideSaleFrom, DateTime? overrideSaleTo,
        DateTime utcNow)
    {
        var effectiveSaleFrom = overrideSaleFrom ?? saleFrom;
        var effectiveSaleTo = overrideSaleTo ?? saleTo;
        if (overrideSalePrice is > 0m && IsSaleActive(effectiveSaleFrom, effectiveSaleTo, utcNow))
            return overrideSalePrice.Value;
        if (overridePrice is > 0m)
            return overridePrice.Value;
        if (salePrice is > 0m && IsSaleActive(saleFrom, saleTo, utcNow))
            return salePrice.Value;
        return price ?? 0m;
    }

    /// <summary>The price of a variant: site variant override (sale → price) → variant sale price → variant price.</summary>
    public static decimal ResolveVariantPrice(decimal? price, decimal? salePrice, decimal? overridePrice, decimal? overrideSalePrice)
    {
        if (overrideSalePrice is > 0m) return overrideSalePrice.Value;
        if (overridePrice is > 0m) return overridePrice.Value;
        if (salePrice is > 0m) return salePrice.Value;
        return price ?? 0m;
    }

    public static bool IsSaleActive(DateTime? from, DateTime? to, DateTime utcNow)
    {
        if (from.HasValue && utcNow < from.Value) return false;
        if (to.HasValue && utcNow > to.Value) return false;
        return true;
    }

    public static decimal Round2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
    public static decimal Round4(decimal v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);
    private static decimal Clamp2(decimal v) => Math.Max(0m, Round2(v));
}
