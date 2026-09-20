using System.Text.Json;
using George.Data;
using George.DB;
using George.Services;
using George.Services.Bundles;
using George.Services.Request;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Bundles (מארזים) order-line logic - BUNDLES_SYNC_SPEC.md §2 / §3.3 / §4 / §6: parent + child expansion,
/// re-pricing after picking, slot validation, stock consumption and the outgoing `bundle` payload block.
/// </summary>
public class BundleOrderLineBuilderTests
{
    private static Product WeightProduct(int id, string name) => new()
    {
        Id = id,
        Name = name,
        SetupType = new SetupType { Id = 1, Name = "by_weight" },
        IsWeighted = true,
    };

    private static ProductBundleComponent Slot(int id, int productId, decimal qty, string? unit = null, params ProductBundleComponentSwap[] swaps)
    {
        var slot = new ProductBundleComponent
        {
            Id = id,
            ComponentProductId = productId,
            Qty = qty,
            SortOrder = id,
            ComponentKey = "c" + id,
            Unit = unit,
            ComponentProduct = new Product { Id = productId, Name = "P" + productId },
            // Tests exercise swaps; a real definition marks such a slot swappable (the API rejects swaps on one that is not).
            Swappable = true,
        };
        foreach (var s in swaps)
        {
            s.ComponentId = id;
            slot.Swaps.Add(s);
        }
        return slot;
    }

    private static List<BundleLineSlot> TwoSlots() => new()
    {
        new BundleLineSlot { ComponentId = 12, SlotIndex = 0, ProductId = 2293, Title = "אנטרקוט", QtyPerBundle = 1m, IsWeight = true, UnitPrice = 120m, Product = WeightProduct(2293, "אנטרקוט") },
        new BundleLineSlot { ComponentId = 13, SlotIndex = 1, ProductId = 2300, Title = "נקניקיות", QtyPerBundle = 4m, IsWeight = false, UnitPrice = 10m, SwappedFromProductId = 2299, Surcharge = 5m },
    };

    // ───────── expansion (§2 / §3.3) ─────────

    [Fact]
    public void Expand_FixedMode_ParentAndChildrenHaveBundleSemantics()
    {
        var parent = new OrderItem { ProductId = 10, Title = "מארז על האש", PricePerUnit = 204m, TotalPrice = 408m };
        var exp = BundleOrderLineBuilder.Expand(parent, 2m, TwoSlots(), "fixed", "none", 0m, firstSortOrder: 3);

        Assert.Equal(10, exp.Parent.BundleProductId);
        Assert.True(BundleOrderLines.IsBundleParent(exp.Parent));
        Assert.Equal(2m, exp.Parent.Quantity);
        Assert.Equal(2m, exp.Parent.PickedQuantity);                // mirrors the bundles - not pickable
        Assert.False(exp.Parent.PickingUserConfirmed);
        Assert.Equal(3, exp.Parent.SortOrder);
        Assert.Equal("units", exp.Parent.OrderLineQuantityMode);
        Assert.Equal(2, exp.Children.Count);

        var kg = exp.Children[0];
        Assert.True(BundleOrderLines.IsBundleChild(kg));           // by navigation, before the first save
        Assert.Same(exp.Parent, kg.ParentOrderItem);
        Assert.Equal(2m, kg.Quantity);                              // 1 kg × 2 bundles
        Assert.Equal("weight", kg.OrderLineQuantityMode);
        Assert.Equal(1000m, kg.UnitWeightGrams);
        Assert.Equal(120m, kg.PricePerUnit);
        Assert.Null(kg.TotalPrice);                                 // fixed mode: no share
        Assert.Equal(12, kg.BundleComponentId);
        Assert.Equal(0, kg.BundleComponentIndex);
        Assert.Equal(4, kg.SortOrder);
        Assert.Null(kg.SwappedFromProductId);
        Assert.Null(kg.SwapSurcharge);

        var units = exp.Children[1];
        Assert.Equal(8m, units.Quantity);                           // 4 × 2
        Assert.Equal("units", units.OrderLineQuantityMode);
        Assert.Equal(2299, units.SwappedFromProductId);
        Assert.Equal(5m, units.SwapSurcharge);
        Assert.Equal(5, units.SortOrder);
        Assert.False(units.PickingUserConfirmed);

        // Parent money untouched by the expansion.
        Assert.Equal(204m, exp.Parent.PricePerUnit);
        Assert.Equal(408m, exp.Parent.TotalPrice);
        Assert.Equal(3, exp.All.Count());
    }

    [Fact]
    public void Expand_SumMode_ChildSharesFollowRatioAndSumToParentTotal()
    {
        // raw = 120×1 + 10×4 = 160 ; 10% discount → base 144 ; + surcharge 5 → unit 149 ; 2 bundles → 298
        var parent = new OrderItem { ProductId = 10, PricePerUnit = 149m, TotalPrice = 298m };
        var exp = BundleOrderLineBuilder.Expand(parent, 2m, TwoSlots(), "sum", "percent", 10m, 0, basePrice: 144m);

        var ratio = 144m / 160m;
        Assert.Equal(Math.Round(120m * 2m * ratio, 2), exp.Children[0].TotalPrice);
        Assert.Equal(Math.Round(10m * 8m * ratio + 5m * 2m, 2), exp.Children[1].TotalPrice);
        Assert.Equal(298m, exp.Children.Sum(c => c.TotalPrice ?? 0m));
    }

    [Fact]
    public void Expand_SumMode_SwappedSlot_SharesStillSumToParent()
    {
        // Slot 1 swapped to a dearer product (₪95/kg in a 2-unit slot): the parent stays engine-priced
        // (base 83.25 + surcharge 7 = 90.25); the children split exactly that, never price × qty of the swap.
        var slots = new List<BundleLineSlot>
        {
            new() { ComponentId = 1, SlotIndex = 0, ProductId = 3814, Title = "קרפיון", QtyPerBundle = 0.4m, IsWeight = true, UnitPrice = 95m, SwappedFromProductId = 3802, Surcharge = 7m },
            new() { ComponentId = 2, SlotIndex = 1, ProductId = 3813, Title = "דג טחון", QtyPerBundle = 0.5m, IsWeight = true, UnitPrice = 69m },
        };
        var parent = new OrderItem { ProductId = 10, Quantity = 1m, PricePerUnit = 90.25m, TotalPrice = 90.25m };
        var exp = BundleOrderLineBuilder.Expand(parent, 1m, slots, "sum", "percent", 10m, 0, basePrice: 83.25m);
        Assert.Equal(90.25m, exp.Children.Sum(c => c.TotalPrice ?? 0m));
        // base 83.25 split by catalog value 38 : 34.5, plus the surcharge on the swapped slot
        Assert.Equal(Math.Round(83.25m * 38m / 72.5m + 7m, 2), exp.Children[0].TotalPrice);
        Assert.True(exp.Children[1].TotalPrice > 0m);
    }

    [Fact]
    public void Expand_ExplicitLineQuantityOverridesSlotQty()
    {
        var slots = TwoSlots();
        slots[0].LineQuantityOverride = 2.35m;
        var exp = BundleOrderLineBuilder.Expand(new OrderItem { ProductId = 10 }, 2m, slots, "fixed", "none", 0m, 0);
        Assert.Equal(2.35m, exp.Children[0].Quantity);
    }

    // ───────── picking (§3.3 / §4) ─────────

    [Fact]
    public void ApplyPickingRules_ParentConfirmedOnlyWhenAllChildrenConfirmed()
    {
        var parent = new OrderItem { Id = 1, ProductId = 10, BundleProductId = 10, Quantity = 2m, PricePerUnit = 149m, TotalPrice = 298m };
        var c1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 2m, PricePerUnit = 120m, PickedQuantity = 2.2m, PickingUserConfirmed = true, DepreciationPercent = 10m };
        var c2 = new OrderItem { Id = 3, ParentOrderItemId = 1, Quantity = 8m, PricePerUnit = 10m, SwapSurcharge = 5m };

        var changed = BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "fixed", false, "none", 0m);

        Assert.False(changed);
        Assert.False(parent.PickingUserConfirmed);
        Assert.Equal(2m, parent.PickedQuantity);
        Assert.Equal(298m, parent.TotalPrice);       // fixed: never changes on picking
        Assert.Null(c1.DepreciationPercent);          // פחת never on bundle lines
        Assert.Null(c1.TotalPrice);

        c2.PickingUserConfirmed = true;
        c2.PickedQuantity = 8m;
        BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "fixed", false, "none", 0m);
        Assert.True(parent.PickingUserConfirmed);
    }

    [Fact]
    public void ApplyPickingRules_SumReweigh_RepricesParentFromWeighedComponents()
    {
        // base 144 (unit 149 − surcharge 5), raw 160 → ratio 0.9
        var parent = new OrderItem { Id = 1, ProductId = 10, BundleProductId = 10, Quantity = 2m, PricePerUnit = 149m, TotalPrice = 298m };
        var c1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 2m, PricePerUnit = 120m, PickedQuantity = 2.5m, PickingUserConfirmed = true };
        var c2 = new OrderItem { Id = 3, ParentOrderItemId = 1, Quantity = 8m, PricePerUnit = 10m, SwapSurcharge = 5m, PickedQuantity = 8m, PickingUserConfirmed = true };

        var changed = BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "sum", true, "percent", 10m);

        var share1 = Math.Round(120m * 2.5m * 0.9m, 2);       // 270.00
        var share2 = Math.Round(10m * 8m * 0.9m + 5m * 2m, 2); // 82.00
        Assert.True(changed);
        Assert.Equal(share1, c1.TotalPrice);
        Assert.Equal(share2, c2.TotalPrice);
        Assert.Equal(share1 + share2, parent.TotalPrice);
        Assert.True(parent.PickingUserConfirmed);
    }

    [Fact]
    public void ApplyPickingRules_Reweigh_WeighedPieceLine_ChargesTheWeighedKgAtThePerKgPrice()
    {
        // 4 portions of ~0.2 kg (₪145/kg → ₪29 a piece) ordered; the picker weighed 0.9 kg (stored as kg, like a
        // regular order line) → 4.5 pieces × 29 = 145 × 0.9 = 130.50, not 29 × 0.9.
        var parent = new OrderItem { Id = 1, BundleProductId = 10, Quantity = 1m, PricePerUnit = 116m, TotalPrice = 116m };
        var c1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 4m, OrderLineQuantityMode = "units", UnitWeightGrams = 200m, PricePerUnit = 29m, PickedQuantity = 0.9m, PickingUserConfirmed = true };
        Assert.Equal(4.5m, BundleOrderLineBuilder.PickedQuantityInSlotUnit(c1));
        Assert.Equal(0.9m, BundleOrderLineBuilder.SlotUnitQuantityToPicked(c1, 4.5m));
        BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1 }, "sum", true, "none", 0m);
        Assert.Equal(130.5m, parent.TotalPrice);

        // A kg line and a plain pieces line are unchanged.
        var kg = new OrderItem { OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, PickedQuantity = 1.2m };
        Assert.Equal(1.2m, BundleOrderLineBuilder.PickedQuantityInSlotUnit(kg));
        var pcs = new OrderItem { OrderLineQuantityMode = "units", PickedQuantity = 3m };
        Assert.Equal(3m, BundleOrderLineBuilder.PickedQuantityInSlotUnit(pcs));
    }

    [Fact]
    public void ApplyPickingRules_Reweigh_KeepsTheStoreCouponAndASwapDoesNotRepriceByItself()
    {
        // Store order: catalog contents 100 (2 kg × 30 + 1 kg × 40), the customer paid 90 after a 10% COUPON
        // (the bundle itself has no discount). Weighing exactly what was ordered must give exactly 90 back.
        var parent = new OrderItem { Id = 1, BundleProductId = 10, Quantity = 1m, PricePerUnit = 90m, TotalPrice = 90m };
        var c1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 2m, OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, PricePerUnit = 30m, PickedQuantity = 2m, PickingUserConfirmed = true };
        var c2 = new OrderItem { Id = 3, ParentOrderItemId = 1, Quantity = 1m, OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, PricePerUnit = 40m, PickedQuantity = 1m, PickingUserConfirmed = true };
        BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "sum", true, "none", 0m);
        Assert.Equal(90m, parent.TotalPrice);

        // 10% more of the first component: only that share grows, still at the coupon's ratio (60 → 66, × 0.9).
        c1.PickedQuantity = 2.2m;
        BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "sum", true, "none", 0m);
        Assert.Equal(95.4m, parent.TotalPrice);

        // A dearer product swapped into slot 2 (surcharge 5 → the customer pays 95): ordered quantities = 95, not a catalog re-price.
        var swapped = new OrderItem { Id = 1, BundleProductId = 10, Quantity = 1m, PricePerUnit = 95m, TotalPrice = 95m };
        var s1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 2m, OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, PricePerUnit = 30m, PickedQuantity = 2m, PickingUserConfirmed = true };
        var s2 = new OrderItem { Id = 3, ParentOrderItemId = 1, Quantity = 1m, OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, PricePerUnit = 80m, SwapSurcharge = 5m, SwappedFromProductId = 7, PickedQuantity = 1m, PickingUserConfirmed = true };
        BundleOrderLineBuilder.ApplyPickingRules(swapped, new[] { s1, s2 }, "sum", true, "none", 0m);
        Assert.Equal(95m, swapped.TotalPrice);
    }

    [Fact]
    public void ApplyPickingRules_SumWithoutReweigh_KeepsParentTotal()
    {
        var parent = new OrderItem { Id = 1, BundleProductId = 10, Quantity = 1m, PricePerUnit = 149m, TotalPrice = 149m };
        var c1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 1m, PricePerUnit = 120m, PickedQuantity = 3m, PickingUserConfirmed = true };
        BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1 }, "sum", false, "none", 0m);
        Assert.Equal(149m, parent.TotalPrice);
    }

    // ───────── slot validation (§3.3 swap) ─────────

    [Fact]
    public void ResolveSlotSelection_OriginalConfiguredSwapAndFreeSwap()
    {
        var slot = Slot(12, 2293, 1m, "kg", new ProductBundleComponentSwap { Id = 3, SwapProductId = 2313, Surcharge = 30m });

        Assert.Null(BundleOrderLineBuilder.ResolveSlotSelection(slot, 2293, null, false, 99m, out var s0, out var swap0, out _, out _));
        Assert.Equal(0m, s0);
        Assert.False(swap0);

        Assert.Null(BundleOrderLineBuilder.ResolveSlotSelection(slot, 2313, null, false, 99m, out var s1, out var swap1, out var free1, out _));
        Assert.Equal(30m, s1);                 // configured surcharge wins over the requested one
        Assert.True(swap1);
        Assert.False(free1);

        var err = BundleOrderLineBuilder.ResolveSlotSelection(slot, 5000, null, false, 12m, out _, out _, out _, out _);
        Assert.Equal(BundleOrderLineBuilder.ErrFreeSwapNotAllowed, err);

        Assert.Null(BundleOrderLineBuilder.ResolveSlotSelection(slot, 5000, null, true, 12m, out var s2, out var swap2, out var free2, out _));
        Assert.Equal(12m, s2);
        Assert.True(swap2);
        Assert.True(free2);
    }

    [Fact]
    public void ResolveSlotSelection_SlotNotMarkedSwappable_RejectsEverythingButItsOwnProduct()
    {
        var slot = Slot(12, 2293, 1m, "kg", new ProductBundleComponentSwap { Id = 3, SwapProductId = 2313, Surcharge = 30m });
        slot.Swappable = false;

        Assert.Null(BundleOrderLineBuilder.ResolveSlotSelection(slot, 2293, null, true, null, out _, out _, out _, out _));
        // Neither a listed swap nor a free swap (site flag on) gets past "ניתן להחלפה" = off.
        Assert.Equal(BundleOrderLineBuilder.ErrSlotNotSwappable, BundleOrderLineBuilder.ResolveSlotSelection(slot, 2313, null, false, null, out _, out _, out _, out _));
        Assert.Equal(BundleOrderLineBuilder.ErrSlotNotSwappable, BundleOrderLineBuilder.ResolveSlotSelection(slot, 5000, null, true, 12m, out _, out _, out _, out _));
    }

    [Fact]
    public void ConvertSwapQuantity_ReExpressesTheQuantityInTheNewProductsUnit()
    {
        // 4 portions of ~0.2 kg → a kg product: 0.8 kg; 1 kg → ~0.2 kg portions: 5 units; 0.7 kg → 4 units (rounded, ≥ 1).
        Assert.Equal(0.8m, BundleOrderLineBuilder.ConvertSwapQuantity(4m, false, 0.2m, true, null));
        Assert.Equal(5m, BundleOrderLineBuilder.ConvertSwapQuantity(1m, true, null, false, 0.2m));
        Assert.Equal(4m, BundleOrderLineBuilder.ConvertSwapQuantity(0.7m, true, null, false, 0.2m));
        Assert.Equal(1m, BundleOrderLineBuilder.ConvertSwapQuantity(0.1m, true, null, false, 0.5m));
        // Same kind of unit: unchanged. No usable weight: the number is kept (kg → whole units).
        Assert.Equal(1.5m, BundleOrderLineBuilder.ConvertSwapQuantity(1.5m, true, null, true, 0.2m));
        Assert.Equal(3m, BundleOrderLineBuilder.ConvertSwapQuantity(3m, false, null, false, null));
        Assert.Equal(3m, BundleOrderLineBuilder.ConvertSwapQuantity(3m, false, null, true, null));
        Assert.Equal(2m, BundleOrderLineBuilder.ConvertSwapQuantity(1.6m, true, null, false, null));
    }

    [Fact]
    public void ResolveSlotSelection_ConfiguredProductWithoutVariant_IsTheConfiguredProductWithItsVariant()
    {
        var slot = Slot(12, 2293, 1m, "kg");
        slot.ComponentVariantId = 77;

        // Client sends the configured product id with productVariantId null → not a free swap; the slot's variant is filled.
        Assert.Null(BundleOrderLineBuilder.ResolveSlotSelection(slot, 2293, null, false, 99m, out var s, out var isSwap, out var isFree, out var variantId));
        Assert.Equal(0m, s);
        Assert.False(isSwap);
        Assert.False(isFree);
        Assert.Equal(77, variantId);

        // The configured variant explicitly → unchanged.
        Assert.Null(BundleOrderLineBuilder.ResolveSlotSelection(slot, 2293, 77, false, null, out _, out var swapSame, out _, out var sameVariant));
        Assert.False(swapSame);
        Assert.Equal(77, sameVariant);

        // A different variant of the same product is still a swap (free here, so refused without the site flag).
        Assert.Equal(BundleOrderLineBuilder.ErrFreeSwapNotAllowed,
            BundleOrderLineBuilder.ResolveSlotSelection(slot, 2293, 78, false, null, out _, out _, out _, out _));
    }

    [Fact]
    public void ApplyPickingRules_SumReweigh_SkippedWhenAChildHasNoPrice()
    {
        var parent = new OrderItem { Id = 1, ProductId = 10, BundleProductId = 10, Quantity = 2m, PricePerUnit = 149m, TotalPrice = 298m };
        var c1 = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 2m, PricePerUnit = 120m, PickedQuantity = 2.5m, PickingUserConfirmed = true, TotalPrice = 240m };
        var c2 = new OrderItem { Id = 3, ParentOrderItemId = 1, Quantity = 8m, PricePerUnit = null, PickedQuantity = 8m, PickingUserConfirmed = true, TotalPrice = 58m };

        var changed = BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "sum", true, "percent", 10m);

        Assert.False(changed);
        Assert.Equal(298m, parent.TotalPrice);       // ordered total kept - a 0 share would under-charge
        Assert.Equal(240m, c1.TotalPrice);           // child shares untouched
        Assert.Equal(58m, c2.TotalPrice);
        Assert.True(parent.PickingUserConfirmed);    // confirmation / mirror rules still apply
        Assert.Equal(2m, parent.PickedQuantity);

        c2.PricePerUnit = 0m;
        Assert.False(BundleOrderLineBuilder.ApplyPickingRules(parent, new[] { c1, c2 }, "sum", true, "percent", 10m));
        Assert.Equal(298m, parent.TotalPrice);
    }

    [Fact]
    public void ReconcileHeaderTotals_AddsBundleRepricingDeltaToSubTotalAndTotal()
    {
        var reqs = new List<CreateOrderItemReq>
        {
            new() { ProductId = 5, Quantity = 1m, PricePerUnit = 50m, TotalPrice = 50m },
            new() { ProductId = 10, Quantity = 2m, PricePerUnit = 200m, TotalPrice = 400m },   // client's stale bundle price
            new() { ProductId = 10, Quantity = 1m, PricePerUnit = 200m },                      // no total → qty × unit
        };
        var lines = new List<OrderItem>
        {
            new() { ProductId = 5, Quantity = 1m, PricePerUnit = 50m, TotalPrice = 50m },
            new() { ProductId = 10, BundleProductId = 10, Quantity = 2m, PricePerUnit = 229m, TotalPrice = 458m },
            new() { ProductId = 2293, ParentOrderItemId = 2, BundleComponentId = 12 },
            new() { ProductId = 10, BundleProductId = 10, Quantity = 1m, PricePerUnit = 229m, TotalPrice = 229m },
        };
        var order = new Order { SubTotal = 650m, Total = 680m, ShippingCost = 30m };

        OrderService.ReconcileHeaderTotalsForRepricedBundleParents(order, reqs, lines);

        Assert.Equal(650m + 58m + 29m, order.SubTotal);
        Assert.Equal(680m + 58m + 29m, order.Total);

        // No bundle parents / no header → untouched.
        var plain = new Order { SubTotal = 50m, Total = 50m };
        OrderService.ReconcileHeaderTotalsForRepricedBundleParents(plain, reqs.Take(1).ToList(), lines.Take(1).ToList());
        Assert.Equal(50m, plain.SubTotal);
        var noHeader = new Order();
        OrderService.ReconcileHeaderTotalsForRepricedBundleParents(noHeader, reqs, lines);
        Assert.Null(noHeader.SubTotal);
        Assert.Null(noHeader.Total);
    }

    [Fact]
    public void MatchSlot_ByKeyThenIndex_And_GramsBecomeKg()
    {
        var slots = new List<ProductBundleComponent> { Slot(12, 2293, 1m), Slot(13, 2300, 4m) };
        Assert.Equal(13, BundleOrderLineBuilder.MatchSlot(slots, "c13", 0)!.Id);   // key wins
        Assert.Equal(13, BundleOrderLineBuilder.MatchSlot(slots, null, 1)!.Id);    // index fallback
        Assert.Null(BundleOrderLineBuilder.MatchSlot(slots, "zzz", 7));

        var (kg, isWeight) = BundleOrderLineBuilder.NormalizeWooComponentQty(600m, "grams", "weight");
        Assert.Equal(0.6m, kg);
        Assert.True(isWeight);
        var (units, unitsWeight) = BundleOrderLineBuilder.NormalizeWooComponentQty(4m, "unit", "unit");
        Assert.Equal(4m, units);
        Assert.False(unitsWeight);
    }

    [Fact]
    public void RawFromBase_InvertsTheDiscount()
    {
        Assert.Equal(160m, BundleOrderLineBuilder.RawFromBase(144m, "percent", 10m));
        Assert.Equal(170m, BundleOrderLineBuilder.RawFromBase(150m, "fixed", 20m));
        Assert.Equal(150m, BundleOrderLineBuilder.RawFromBase(150m, "none", 0m));
    }

    // ───────── stock / billing helpers (§2) ─────────

    [Fact]
    public void StockConsumption_ChildUsesQuantity_ParentConsumesNothing()
    {
        var parent = new OrderItem { BundleProductId = 10, Quantity = 2m, OrderLineQuantityMode = "units" };
        Assert.Equal(0m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(parent));

        // Weight child in sum mode: TotalPrice is a discounted share - must not be read as kg × ₪/kg.
        var child = new OrderItem { ParentOrderItemId = 1, Quantity = 2m, OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, PricePerUnit = 120m, TotalPrice = 216m };
        Assert.Equal(2m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(child));

        var slices = new OrderItem { ParentOrderItemId = 1, Quantity = 8m, OrderLineQuantityMode = "units", UnitWeightGrams = 200m };
        Assert.Equal(1.6m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(slices));
    }

    [Fact]
    public void OcStoreosBilling_IgnoresChildren()
    {
        var parent = new OrderItem { Id = 1, BundleProductId = 10, Quantity = 1m, PickedQuantity = 1m, PickingUserConfirmed = false, TotalPrice = 149m };
        var child = new OrderItem { Id = 2, ParentOrderItemId = 1, Quantity = 1m, PickedQuantity = 0m, TotalPrice = null, PickingUserConfirmed = true };
        Assert.False(OrderItemLineDisplay.IsOcStoreosBillableLine(child));
        // A partially picked bundle must not switch the order into "after picking" and drop its parent.
        Assert.False(OrderItemLineDisplay.OrderHasOcStoreosPickingAdjustments(new[] { parent, child }));
    }

    // ───────── outgoing payload (§6) ─────────

    [Fact]
    public void BuildOcStoreosBundleObject_MatchesSpecShape()
    {
        var def = new BundleDefinition
        {
            ProductId = 10,
            Config = new ProductBundleConfig { ProductId = 10, PricingMode = "fixed", ReweighPrice = false, InvoiceDisplay = "bundle", DiscountType = "none" },
            Components = new List<ProductBundleComponent>
            {
                Slot(12, 2293, 1m, "kg"),
                Slot(13, 2300, 4m, "unit"),
            },
        };
        def.Components[0].Mode = "weight";
        def.Components[0].Description = "";
        def.Components[1].Mode = "unit";

        var parent = new OrderItem { Id = 100, ProductId = 10, BundleProductId = 10, Quantity = 2m, PricePerUnit = 204m, TotalPrice = 408m, WooLineItemId = 789 };
        var c1 = new OrderItem { Id = 101, ParentOrderItemId = 100, ProductId = 2293, Quantity = 2m, OrderLineQuantityMode = "weight", BundleComponentId = 12, BundleComponentIndex = 0, LineSku = "ENT-01", Title = "אנטרקוט", PickedQuantity = 2.3m, PickingUserConfirmed = true, SortOrder = 1 };
        var c2 = new OrderItem { Id = 102, ParentOrderItemId = 100, ProductId = 2313, Quantity = 8m, OrderLineQuantityMode = "units", BundleComponentId = 13, BundleComponentIndex = 1, Title = "פילה", SwappedFromProductId = 2300, SwapSurcharge = 5m, SortOrder = 2 };
        var wooProducts = new Dictionary<int, int> { [10] = 456, [2293] = 9001, [2313] = 9013, [2300] = 9000 };

        var bundle = WooCommerceService.BuildOcStoreosBundleObject(parent, new[] { c1, c2 }, def, wooProducts, new Dictionary<int, int>());

        Assert.Equal("george-10", bundle["externalId"]);
        Assert.Equal(456, bundle["wooProductId"]);
        Assert.Equal(789, bundle["itemId"]);
        Assert.Equal("fixed", bundle["pricingMode"]);
        Assert.Equal(false, bundle["reweighPrice"]);
        Assert.Equal("bundle", bundle["invoiceDisplay"]);
        Assert.Equal(199m, bundle["basePrice"]);          // 204 − surcharge 5

        var comps = Assert.IsType<List<Dictionary<string, object?>>>(bundle["components"]);
        Assert.Equal(2, comps.Count);
        Assert.Equal(0, comps[0]["index"]);
        Assert.Equal("c12", comps[0]["key"]);
        Assert.Equal(9001, comps[0]["productId"]);
        Assert.Equal(0, comps[0]["variationId"]);
        Assert.Equal("ENT-01", comps[0]["sku"]);
        Assert.Equal(1m, comps[0]["qty"]);
        Assert.Equal("kg", comps[0]["unit"]);
        Assert.Equal("weight", comps[0]["mode"]);
        Assert.Equal(2.3m, comps[0]["actualQty"]);
        Assert.Null(comps[0]["swappedFromProductId"]);
        Assert.Equal(0m, comps[0]["surcharge"]);

        Assert.Equal(9013, comps[1]["productId"]);
        Assert.Equal(9000, comps[1]["swappedFromProductId"]);
        Assert.Equal(5m, comps[1]["surcharge"]);
        Assert.Null(comps[1]["actualQty"]);                // not confirmed yet

        // Serializes to the camelCase §6 keys (dictionary nulls stay as JSON null, like the rest of the push body).
        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        Assert.Contains("\"externalId\":\"george-10\"", json);
        Assert.Contains("\"itemId\":789", json);
        Assert.Contains("\"components\":[{\"index\":0,\"key\":\"c12\"", json);
        Assert.Contains("\"swappedFromVariationId\":null", json);
    }

    [Fact]
    public void BuildOcStoreosBundleObject_UnknownDefinition_OmitsItemIdAndUsesLineIds()
    {
        var parent = new OrderItem { Id = 100, ProductId = 10, BundleProductId = 10, Quantity = 1m, PricePerUnit = 99m, WooCommerceProductId = 456 };
        var child = new OrderItem { Id = 101, ParentOrderItemId = 100, ProductId = 2293, WooCommerceProductId = 9001, Quantity = 0.5m, OrderLineQuantityMode = "weight", BundleComponentIndex = 0 };
        var unitChild = new OrderItem { Id = 102, ParentOrderItemId = 100, ProductId = 2300, WooCommerceProductId = 9000, Quantity = 4m, OrderLineQuantityMode = "units", BundleComponentIndex = 1 };
        var bundle = WooCommerceService.BuildOcStoreosBundleObject(parent, new[] { child, unitChild }, null, new Dictionary<int, int>(), new Dictionary<int, int>());
        Assert.False(bundle.ContainsKey("itemId"));   // omitted = create on the store side
        Assert.Equal(456, bundle["wooProductId"]);
        var comps = Assert.IsType<List<Dictionary<string, object?>>>(bundle["components"]);
        Assert.Equal(9001, comps[0]["productId"]);
        Assert.Null(comps[0]["key"]);
        Assert.Equal(0.5m, comps[0]["qty"]);
        Assert.Equal("weight", comps[0]["mode"]);
        Assert.Equal("units", comps[1]["mode"]);      // plugin vocabulary: units | units_weight | weight
        Assert.Equal("unit", comps[1]["unit"]);
    }

    [Fact]
    public void BuildOcStoreosBundleObject_GramsSlot_SendsActualQtyInGrams_AndSwappedFromVariation()
    {
        var def = new BundleDefinition
        {
            ProductId = 10,
            Config = new ProductBundleConfig { ProductId = 10, PricingMode = "fixed", DiscountType = "none" },
            Components = new List<ProductBundleComponent>
            {
                Slot(12, 2293, 0.6m, "grams"),
                Slot(13, 2300, 1m, "unit"),
            },
        };
        def.Components[0].Mode = "weight";
        def.Components[1].Mode = "units";
        def.Components[1].ComponentVariantId = 55;   // configured original is a variant

        var parent = new OrderItem { Id = 100, ProductId = 10, BundleProductId = 10, Quantity = 1m, PricePerUnit = 150m, TotalPrice = 150m };
        // George stores kg: 0.6 kg weighed → the plugin reads actualQty in the emitted unit (grams) → 600.
        var c1 = new OrderItem { Id = 101, ParentOrderItemId = 100, ProductId = 2293, Quantity = 0.6m, OrderLineQuantityMode = "weight", BundleComponentId = 12, BundleComponentIndex = 0, PickedQuantity = 0.6m, PickingUserConfirmed = true };
        var c2 = new OrderItem { Id = 102, ParentOrderItemId = 100, ProductId = 2313, Quantity = 1m, OrderLineQuantityMode = "units", BundleComponentId = 13, BundleComponentIndex = 1, SwappedFromProductId = 2300, SwapSurcharge = 5m };
        var wooProducts = new Dictionary<int, int> { [10] = 456, [2293] = 9001, [2313] = 9013, [2300] = 9000 };
        var wooVariations = new Dictionary<int, int> { [55] = 7055 };

        var bundle = WooCommerceService.BuildOcStoreosBundleObject(parent, new[] { c1, c2 }, def, wooProducts, wooVariations);
        var comps = Assert.IsType<List<Dictionary<string, object?>>>(bundle["components"]);

        Assert.Equal("grams", comps[0]["unit"]);
        Assert.Equal(600m, comps[0]["actualQty"]);
        Assert.Null(comps[0]["swappedFromVariationId"]);

        Assert.Equal(9000, comps[1]["swappedFromProductId"]);
        Assert.Equal(7055, comps[1]["swappedFromVariationId"]);
        Assert.Null(comps[1]["actualQty"]);

        // A kg slot keeps kg.
        def.Components[0].Unit = "kg";
        var kgBundle = WooCommerceService.BuildOcStoreosBundleObject(parent, new[] { c1, c2 }, def, wooProducts, wooVariations);
        var kgComps = Assert.IsType<List<Dictionary<string, object?>>>(kgBundle["components"]);
        Assert.Equal(0.6m, kgComps[0]["actualQty"]);
    }
}
