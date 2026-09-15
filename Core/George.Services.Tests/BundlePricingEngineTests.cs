using George.Data;
using George.DB;
using George.Services.Bundles;

namespace George.Services.Tests;

/// <summary>BUNDLES_SYNC_SPEC.md §4 - the George pricing engine must mirror class-oc-bundles-pricing.php.</summary>
public class BundlePricingEngineTests
{
    private static BundlePricingComponent Comp(int id, decimal qty, decimal price, decimal surcharge = 0m) => new()
    {
        ComponentId = id,
        ProductId = 1000 + id,
        Qty = qty,
        OriginalUnitPrice = price,
        Surcharge = surcharge,
    };

    [Fact]
    public void Fixed_NoDiscount_UnitPriceIsFixedPrice()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "fixed",
            FixedPrice = 199m,
            Components = { Comp(1, 1m, 120m), Comp(2, 0.5m, 200m) },
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(199m, r.RawPrice);
        Assert.Equal(199m, r.BasePrice);
        Assert.Equal(199m, r.UnitPrice);
        Assert.Equal(0m, r.DiscountAmount);
        Assert.Equal(199m, r.LineTotal);
        // Fixed mode: component shares are informational-only and stay null.
        Assert.All(r.Components, c => Assert.Null(c.Share));
    }

    [Fact]
    public void Sum_RawIsSumOfOriginalPricesTimesQty()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "sum",
            Components = { Comp(1, 1m, 120m), Comp(2, 0.5m, 200m) }, // 120 + 100
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(220m, r.RawPrice);
        Assert.Equal(220m, r.BasePrice);
        Assert.Equal(220m, r.UnitPrice);
    }

    [Fact]
    public void Sum_IgnoresFixedPrice()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "sum",
            FixedPrice = 999m,
            Components = { Comp(1, 2m, 50m) },
        };
        Assert.Equal(100m, BundlePricingEngine.RawPrice(input));
    }

    [Fact]
    public void PercentDiscount_AppliesToRaw_RoundedTo2()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "sum",
            DiscountType = "percent",
            DiscountValue = 10m,
            Components = { Comp(1, 1m, 220m) },
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(220m, r.RawPrice);
        Assert.Equal(198m, r.BasePrice);
        Assert.Equal(22m, r.DiscountAmount);
        Assert.Equal(198m, r.UnitPrice);
    }

    [Fact]
    public void PercentDiscount_RoundsHalfAwayFromZero()
    {
        // 33.33 × 0.85 = 28.3305 → 28.33 ; 10.005 style midpoint checked via 20.01 × 0.975 = 19.50975 → 19.51
        var input = new BundlePricingInput
        {
            PricingMode = "fixed",
            FixedPrice = 20.01m,
            DiscountType = "percent",
            DiscountValue = 2.5m,
        };
        Assert.Equal(19.51m, BundlePricingEngine.BasePrice(input));
    }

    [Fact]
    public void FixedDiscount_SubtractsValue()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "fixed",
            FixedPrice = 199m,
            DiscountType = "fixed",
            DiscountValue = 20m,
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(179m, r.BasePrice);
        Assert.Equal(20m, r.DiscountAmount);
    }

    [Fact]
    public void FixedDiscount_LargerThanRaw_ClampsAtZero()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "sum",
            DiscountType = "fixed",
            DiscountValue = 500m,
            Components = { Comp(1, 1m, 120m) },
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(120m, r.RawPrice);
        Assert.Equal(0m, r.BasePrice);
        Assert.Equal(120m, r.DiscountAmount); // discount never exceeds raw
        Assert.Equal(0m, r.UnitPrice);
        Assert.Equal(0m, r.LineTotal);
    }

    [Fact]
    public void NegativeRaw_ClampsAtZero()
    {
        var input = new BundlePricingInput { PricingMode = "fixed", FixedPrice = -5m };
        Assert.Equal(0m, BundlePricingEngine.RawPrice(input));
    }

    [Fact]
    public void DiscountNone_OrZeroValue_LeavesRawUntouched()
    {
        Assert.Equal(100m, BundlePricingEngine.ApplyDiscount(100m, "none", 50m));
        Assert.Equal(100m, BundlePricingEngine.ApplyDiscount(100m, "percent", 0m));
        Assert.Equal(100m, BundlePricingEngine.ApplyDiscount(100m, "fixed", 0m));
        Assert.Equal(100m, BundlePricingEngine.ApplyDiscount(100m, null, 10m));
    }

    [Fact]
    public void Surcharges_AddedAfterDiscount_AndLineTotalUsesBundleQty()
    {
        // Spec example: base 199, swap surcharge 30 → unit 229, ×2 bundles = 458.
        var input = new BundlePricingInput
        {
            PricingMode = "fixed",
            FixedPrice = 220m,
            DiscountType = "percent",
            DiscountValue = 9.545454m, // 220 → 199.0000112 → 199.00
            BundleQty = 2m,
            Components = { Comp(12, 1m, 120m, surcharge: 30m), Comp(13, 1m, 100m) },
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(220m, r.RawPrice);
        Assert.Equal(199m, r.BasePrice);
        Assert.Equal(30m, r.SurchargeTotal);
        Assert.Equal(229m, r.UnitPrice);
        Assert.Equal(458m, r.LineTotal);
        var swapped = r.Components.Single(c => c.ComponentId == 12);
        Assert.Equal(2m, swapped.LineQty);
        Assert.Equal(30m, swapped.Surcharge);
    }

    [Fact]
    public void SurchargeIsNotDiscounted()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "sum",
            DiscountType = "percent",
            DiscountValue = 50m,
            Components = { Comp(1, 1m, 100m, surcharge: 40m) },
        };
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(50m, r.BasePrice);
        Assert.Equal(90m, r.UnitPrice); // 50 + 40, the surcharge is never halved
    }

    [Fact]
    public void Sum_ComponentShares_FollowDiscountRatioPlusSurcharge()
    {
        var input = new BundlePricingInput
        {
            PricingMode = "sum",
            DiscountType = "percent",
            DiscountValue = 10m,
            BundleQty = 2m,
            Components = { Comp(1, 1m, 100m), Comp(2, 2m, 50m, surcharge: 5m) },
        };
        var r = BundlePricingEngine.Price(input);
        // raw 200, base 180, ratio 0.9; line = 2 bundles
        Assert.Equal(180m, r.Components[0].Share); // 100 × 1 × 2 × 0.9
        Assert.Equal(190m, r.Components[1].Share); // 50 × 2 × 2 × 0.9 + 5 × 2
    }

    [Fact]
    public void Reweigh_RatioIsBaseOverRaw_SharesUsePickedQty()
    {
        // raw 220 → base 198 (10 % off): ratio 0.9
        var picked = new[]
        {
            new BundleReweighComponent { ComponentId = 1, UnitPrice = 120m, PickedQty = 1.2m },            // 144 × 0.9 = 129.6
            new BundleReweighComponent { ComponentId = 2, UnitPrice = 200m, PickedQty = 0.55m, Surcharge = 30m }, // 110 × 0.9 + 30 × 2 = 159
        };
        var r = BundlePricingEngine.Reweigh(basePrice: 198m, rawPrice: 220m, bundleQty: 2m, picked);
        Assert.Equal(0.9m, r.Ratio);
        Assert.Equal(129.6m, r.Shares[1]);
        Assert.Equal(159m, r.Shares[2]);
        Assert.Equal(288.6m, r.ParentTotal);
    }

    [Fact]
    public void Reweigh_RawZero_RatioIsOne()
    {
        var picked = new[] { new BundleReweighComponent { ComponentId = 1, UnitPrice = 80m, PickedQty = 0.5m } };
        var r = BundlePricingEngine.Reweigh(basePrice: 0m, rawPrice: 0m, bundleQty: 1m, picked);
        Assert.Equal(1m, r.Ratio);
        Assert.Equal(40m, r.ParentTotal);
    }

    [Fact]
    public void Reweigh_ParentTotal_NeverNegative()
    {
        var picked = new[] { new BundleReweighComponent { ComponentId = 1, UnitPrice = -80m, PickedQty = 1m } };
        var r = BundlePricingEngine.Reweigh(basePrice: 10m, rawPrice: 10m, bundleQty: 1m, picked);
        Assert.Equal(0m, r.ParentTotal);
    }

    [Fact]
    public void ResolveProductPrice_SiteOverrideWinsThenSaleInWindowThenPrice()
    {
        var now = new DateTime(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);
        // override sale in window
        Assert.Equal(90m, BundlePricingEngine.ResolveProductPrice(120m, 100m, null, null, 110m, 90m, now.AddDays(-1), now.AddDays(1), now));
        // override sale expired → override price
        Assert.Equal(110m, BundlePricingEngine.ResolveProductPrice(120m, 100m, null, null, 110m, 90m, now.AddDays(-5), now.AddDays(-1), now));
        // no override → product sale in window
        Assert.Equal(100m, BundlePricingEngine.ResolveProductPrice(120m, 100m, now.AddDays(-1), now.AddDays(1), null, null, null, null, now));
        // product sale not started → price
        Assert.Equal(120m, BundlePricingEngine.ResolveProductPrice(120m, 100m, now.AddDays(1), null, null, null, null, null, now));
        // nothing → 0
        Assert.Equal(0m, BundlePricingEngine.ResolveProductPrice(null, null, null, null, null, null, null, null, now));
    }

    [Fact]
    public void ResolveVariantPrice_OverrideThenSaleThenPrice()
    {
        Assert.Equal(70m, BundlePricingEngine.ResolveVariantPrice(100m, 80m, 90m, 70m));
        Assert.Equal(90m, BundlePricingEngine.ResolveVariantPrice(100m, 80m, 90m, null));
        Assert.Equal(80m, BundlePricingEngine.ResolveVariantPrice(100m, 80m, null, null));
        Assert.Equal(100m, BundlePricingEngine.ResolveVariantPrice(100m, null, null, null));
    }

    [Fact]
    public void BuildPricingInput_UsesOriginalPricesAndSwapSurcharges()
    {
        var def = new BundleDefinition
        {
            ProductId = 10,
            Config = new ProductBundleConfig { ProductId = 10, PricingMode = "sum", DiscountType = "none" },
            // Live catalog products on the slots: BuildPricingInput prices a slot whose ComponentProduct is
            // missing/deleted at 0 (the loader always includes the nav; null = hard-deleted product).
            Components =
            {
                new ProductBundleComponent { Id = 12, BundleProductId = 10, ComponentProductId = 2293, Qty = 1m, SortOrder = 0, ComponentProduct = new Product { Id = 2293, Name = "א" } },
                new ProductBundleComponent { Id = 13, BundleProductId = 10, ComponentProductId = 2294, Qty = 0.5m, SortOrder = 1, ComponentProduct = new Product { Id = 2294, Name = "ב" } },
            },
        };
        var prices = new Dictionary<(int ProductId, int? VariantId), decimal>
        {
            [(2293, null)] = 120m,
            [(2294, null)] = 200m,
            [(2313, null)] = 300m, // the swap's own price must NOT change raw
        };
        var swaps = new Dictionary<int, BundleService.BundleSlotSwap>
        {
            [12] = new BundleService.BundleSlotSwap { ProductId = 2313, Surcharge = 30m },
        };
        var input = BundleService.BuildPricingInput(def, prices, 2m, swaps);
        var r = BundlePricingEngine.Price(input);
        Assert.Equal(220m, r.RawPrice);
        Assert.Equal(250m, r.UnitPrice);
        Assert.Equal(500m, r.LineTotal);
        Assert.Equal(2313, r.Components[0].ProductId);
    }
}

/// <summary>OC Bundles sync helpers (BUNDLES_SYNC_SPEC.md §7 step 2).</summary>
public class BundleWooSyncHelpersTests
{
    private static BundleDefinition Definition() => new()
    {
        ProductId = 10,
        Config = new ProductBundleConfig
        {
            ProductId = 10,
            PricingMode = "fixed",
            FixedPrice = 199m,
            DiscountType = "none",
            OosBehavior = "swap",
            Layout = "grid",
            CartDisplay = "name_with_components",
            InvoiceDisplay = "bundle",
        },
        Components =
        {
            new ProductBundleComponent
            {
                Id = 12, ComponentKey = "c12", ComponentProductId = 2293, Qty = 1m, Swappable = true,
                ComponentProduct = new Product { Id = 2293, Name = "אנטרקוט" },
                Swaps = { new ProductBundleComponentSwap { Id = 3, SwapProductId = 2313, Surcharge = 30m, SwapProduct = new Product { Id = 2313, Name = "פילה" } } },
            },
        },
    };

    [Fact]
    public void BuildPutBody_ResolvesWooIdsAndExternalId()
    {
        var body = WooCommerceService.BuildOcBundlesPutBody(
            10, Definition(),
            new Dictionary<int, int> { [2293] = 501, [2313] = 502 },
            new Dictionary<int, int>(),
            out var error);
        Assert.Null(error);
        Assert.NotNull(body);
        Assert.Equal("george-10", body!["external_id"]);
        var components = Assert.IsType<List<object>>(body["components"]);
        var first = Assert.IsType<Dictionary<string, object?>>(components[0]);
        Assert.Equal("c12", first["key"]);
        Assert.Equal(501, first["product_id"]);
        Assert.Equal(0, first["variation_id"]);
        var swaps = Assert.IsType<List<object>>(first["swaps"]);
        var swap = Assert.IsType<Dictionary<string, object?>>(swaps[0]);
        Assert.Equal(502, swap["product_id"]);
        Assert.Equal(30m, swap["surcharge"]);
        var pricing = Assert.IsType<Dictionary<string, object?>>(body["pricing"]);
        Assert.Equal("fixed", pricing["mode"]);
        Assert.Equal(199m, pricing["fixed_price"]);
    }

    [Fact]
    public void BuildPutBody_SendsKgUnitForWeightSlots_AndSwapsOnlyForSwappableSlots()
    {
        var def = Definition();
        def.Components[0].Unit = "grams";                       // Woo mirror says grams, George stores kg → tell Woo "kg"
        def.Components.Add(new ProductBundleComponent
        {
            Id = 13, ComponentKey = "c13", ComponentProductId = 2300, Qty = 4m, SortOrder = 1, Swappable = false,
            ComponentProduct = new Product { Id = 2300, Name = "נקניקיות" },
            // Stale swap row on a non-swappable slot: never sent, and its missing Woo id never blocks the sync.
            Swaps = { new ProductBundleComponentSwap { Id = 4, SwapProductId = 9999, Surcharge = 1m, SwapProduct = new Product { Id = 9999, Name = "לא מסונכרן" } } },
        });

        var body = WooCommerceService.BuildOcBundlesPutBody(
            10, def,
            new Dictionary<int, int> { [2293] = 501, [2313] = 502, [2300] = 503 },
            new Dictionary<int, int>(),
            out var error);

        Assert.Null(error);
        var components = Assert.IsType<List<object>>(body!["components"]);
        var first = Assert.IsType<Dictionary<string, object?>>(components[0]);
        var second = Assert.IsType<Dictionary<string, object?>>(components[1]);
        Assert.Equal("kg", first["unit"]);
        Assert.Single(Assert.IsType<List<object>>(first["swaps"]));
        Assert.Equal("unit", second["unit"]);
        Assert.Equal(false, second["swappable"]);
        Assert.Empty(Assert.IsType<List<object>>(second["swaps"]));
    }

    [Fact]
    public void BuildPutBody_ComponentWithoutWooId_ReturnsHebrewError()
    {
        var body = WooCommerceService.BuildOcBundlesPutBody(
            10, Definition(),
            new Dictionary<int, int> { [2313] = 502 }, // component 2293 missing
            new Dictionary<int, int>(),
            out var error);
        Assert.Null(body);
        Assert.Equal("רכיב אנטרקוט לא מסונכרן לאתר", error);
    }

    [Fact]
    public void BuildPutBody_SwapWithoutWooId_ReturnsHebrewError()
    {
        var body = WooCommerceService.BuildOcBundlesPutBody(
            10, Definition(),
            new Dictionary<int, int> { [2293] = 501 }, // swap 2313 missing
            new Dictionary<int, int>(),
            out var error);
        Assert.Null(body);
        Assert.Equal("רכיב פילה לא מסונכרן לאתר", error);
    }

    [Fact]
    public void MapError_UsesPluginCodeAndMessage()
    {
        var text = WooCommerceService.MapOcBundlesError(400, "{\"code\":\"oc_bundles_invalid_component\",\"message\":\"Product 9 does not exist\",\"data\":{\"status\":400}}");
        Assert.Equal("oc_bundles_invalid_component: Product 9 does not exist", text);
    }

    [Fact]
    public void MapError_NonJson_FallsBackToStatus()
    {
        Assert.Contains("401", WooCommerceService.MapOcBundlesError(401, "<html>denied</html>"));
        Assert.Contains("404", WooCommerceService.MapOcBundlesError(404, ""));
    }

    [Fact]
    public void ParseMirror_MatchesByKeyThenIndex()
    {
        var ordered = new List<ProductBundleComponent>
        {
            new() { Id = 12, ComponentKey = "c12" },
            new() { Id = 13, ComponentKey = "c13" },
        };
        // First element carries no key → matched by index (0 → c12); second is matched by its key.
        var mirror = WooCommerceService.ParseOcBundlesComponentMirror(
            "{\"components\":[{\"unit\":\"kg\",\"mode\":\"weight\",\"unit_weight\":null},{\"key\":\"c13\",\"unit\":\"unit\",\"mode\":\"unit\",\"unit_weight\":\"0.8\"}]}",
            ordered);
        Assert.Equal("kg", mirror[12].Unit);
        Assert.Equal("weight", mirror[12].Mode);
        Assert.Null(mirror[12].UnitWeightKg);
        Assert.Equal("unit", mirror[13].Unit);
        Assert.Equal("unit", mirror[13].Mode);
        Assert.Equal(0.8m, mirror[13].UnitWeightKg);
    }

    [Fact]
    public void ParseAvailability_ReadsSnakeCaseFields()
    {
        var res = WooCommerceService.ParseBundleAvailability(456,
            "{\"price\":\"199.00\",\"available_quantity\":3,\"in_stock\":true,\"components\":[{\"index\":0,\"key\":\"c12\",\"product_id\":501,\"in_stock\":false,\"unit\":\"kg\"}]}");
        Assert.Equal(456, res.WooProductId);
        Assert.Equal(199m, res.Price);
        Assert.Equal(3m, res.AvailableQuantity);
        Assert.True(res.InStock);
        var c = Assert.Single(res.Components);
        Assert.Equal(501, c.ProductId);
        Assert.False(c.InStock);
        Assert.Equal("kg", c.Unit);
    }

    [Fact]
    public void ParseAvailability_ComponentStockIsTheAvailableQuantity_NameAndModeOptional()
    {
        // Plugin component shape: { key, product_id, variation_id, qty, unit, in_stock, stock } - no name / mode.
        var res = WooCommerceService.ParseBundleAvailability(456,
            "{\"price\":199,\"available_quantity\":2,\"in_stock\":true,\"components\":[{\"key\":\"c12\",\"product_id\":501,\"variation_id\":0,\"qty\":1,\"unit\":\"kg\",\"in_stock\":true,\"stock\":2.5}]}");
        var c = Assert.Single(res.Components);
        Assert.Equal("c12", c.Key);
        Assert.Equal(2.5m, c.AvailableQuantity);
        Assert.Null(c.Name);
        Assert.Null(c.Mode);
        Assert.Equal(0, c.VariationId);

        // Older shape still read.
        var legacy = WooCommerceService.ParseBundleAvailability(456, "{\"components\":[{\"available_quantity\":7}]}");
        Assert.Equal(7m, Assert.Single(legacy.Components).AvailableQuantity);
    }
}

public class BundleOrderLinesTests
{
    [Fact]
    public void ParentAndChildClassification()
    {
        var parent = new OrderItem { Id = 1, ProductId = 10, BundleProductId = 10 };
        var child = new OrderItem { Id = 2, ProductId = 2293, ParentOrderItemId = 1, BundleComponentId = 12 };
        var plain = new OrderItem { Id = 3, ProductId = 5 };

        Assert.True(BundleOrderLines.IsBundleParent(parent));
        Assert.False(BundleOrderLines.IsBundleChild(parent));
        Assert.True(BundleOrderLines.IsBundleChild(child));
        Assert.False(BundleOrderLines.IsBundleParent(child));
        Assert.True(BundleOrderLines.IsPlainLine(plain));
        Assert.False(BundleOrderLines.IsBundleParent(null));
    }

    [Fact]
    public void BundleProducts_SetupTypeChecks()
    {
        Assert.True(BundleProducts.IsBundle(new Product { SetupType = new SetupType { Name = "bundle" } }));
        Assert.False(BundleProducts.IsBundle(new Product { SetupType = new SetupType { Name = "by_weight" } }));
        Assert.False(BundleProducts.IsBundle(new Product()));
        Assert.True(BundleProducts.IsWooBundleType("oc_bundle"));
        Assert.False(BundleProducts.IsWooBundleType("simple"));
    }
}
