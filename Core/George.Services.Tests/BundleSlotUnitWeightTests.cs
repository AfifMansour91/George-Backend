using George.Data;
using George.DB;
using George.Services.Bundles;

namespace George.Services.Tests;

/// <summary>
/// BUNDLES_SYNC_SPEC.md §4 - a bundle slot costs exactly what the same product costs on a regular order line:
/// a product priced per kg but sold by units costs price × unit weight per unit.
/// </summary>
public class BundleSlotUnitWeightTests
{
    private static BundleCatalogProductInfo P(string setup, string? unitWeight = null, string? mode = null, string? unit = "kg",
        string? options = null, bool byVariant = false) => new()
    {
        Id = 1,
        SetupType = setup,
        IsWeighted = setup != "standard",
        WeightUnit = unit,
        UnitWeight = unitWeight,
        UnitWeightMode = mode,
        WeightOptions = options,
        WeightByVariant = byVariant,
    };

    [Fact]
    public void Factor_IsOne_ForKgSlotsAndStandardProducts()
    {
        Assert.Equal(1m, BundleSlotUnitWeight.PriceFactor(P("by_weight", "0.2"), null, null));
        Assert.Equal(1m, BundleSlotUnitWeight.PriceFactor(P("standard"), null, null));
        Assert.Equal(1m, BundleSlotUnitWeight.PriceFactor(null, null, null));
    }

    [Fact]
    public void Factor_FixedUnitWeight_KgAndGrams()
    {
        // "פילה סלמון מנות": ₪145 / kg, a unit weighs ~0.2 kg → ₪29 per unit, like the order line.
        Assert.Equal(0.2m, BundleSlotUnitWeight.PriceFactor(P("by_unit", "0.2", "average"), null, null));
        Assert.Equal(0.2m, BundleSlotUnitWeight.PriceFactor(P("by_unit", "200", "average", unit: "g"), null, null));
        Assert.Equal(0.3m, BundleSlotUnitWeight.PriceFactor(P("by_unit_and_weight", "0.3"), null, null));
        // No configured weight: the same 0.5 default the order modal uses.
        Assert.Equal(0.5m, BundleSlotUnitWeight.PriceFactor(P("by_unit"), null, null));
    }

    [Fact]
    public void Factor_ByVariant_UsesVariantWeight()
    {
        var variant = new BundleCatalogVariantInfo { Id = 9, ProductId = 1, Weight = 0.35m };
        Assert.Equal(0.35m, BundleSlotUnitWeight.PriceFactor(P("by_unit", "0.2", "by_variant"), variant, null));
        Assert.Equal(0.35m, BundleSlotUnitWeight.PriceFactor(P("by_unit", "0.2", byVariant: true), variant, null));
        Assert.Equal(0.2m, BundleSlotUnitWeight.PriceFactor(P("by_unit", "0.2", "by_variant"), null, null));
    }

    [Fact]
    public void Factor_VariableWeight_SlotChoiceWins_ElseFirstActiveOption()
    {
        var p = P("by_unit", "0.2", "variable", options: "0.4,0.6##0.9");
        Assert.Equal(0.6m, BundleSlotUnitWeight.PriceFactor(p, null, 0.6m));
        Assert.Equal(0.4m, BundleSlotUnitWeight.PriceFactor(p, null, null));
        Assert.Equal(0.4m, BundleSlotUnitWeight.PriceFactor(P("by_unit", "0.2", "variable", unit: "g", options: "400;600"), null, null));
        Assert.True(BundleSlotUnitWeight.IsVariableWeight(p));
        Assert.False(BundleSlotUnitWeight.IsVariableWeight(P("by_unit", "0.2", "average")));
    }

    [Fact]
    public void PriceBook_SlotPrice_CatalogPrice_LinePrice_AndMerge()
    {
        var book = new BundlePriceBook();
        book.Set((1, null), catalogPrice: 145m, slotUnitPrice: 29m, isVariableWeight: false);
        book.Set((2, null), catalogPrice: 100m, slotUnitPrice: 40m, isVariableWeight: true);

        Assert.Equal(29m, book[(1, null)]);
        Assert.Equal(29m, book.SlotPrice(1, null, 0.9m));          // fixed unit weight: a slot weight never overrides it
        Assert.Equal(60m, book.SlotPrice(2, null, 0.6m));          // choose-a-weight: the slot's own weight
        Assert.Equal(40m, book.SlotPrice(2, null, null));
        Assert.Equal(145m, book.LinePrice(1, null, isWeightLine: true, slotUnitWeightKg: null));
        Assert.Equal(29m, book.LinePrice(1, null, isWeightLine: false, slotUnitWeightKg: null));
        Assert.Equal(0m, book.SlotPrice(99, null, null));

        var other = new BundlePriceBook();
        other.Set((3, 7), 80m, 80m, false);
        book.Merge(other);
        Assert.Equal(80m, book.CatalogPrice(3, 7));
    }

    [Fact]
    public void BuildPricingInput_UnitSlotOfPerKgProduct_CostsLikeARegularOrderLine()
    {
        var def = new BundleDefinition
        {
            ProductId = 500,
            Config = new ProductBundleConfig { PricingMode = "sum", DiscountType = "percent", DiscountValue = 10m },
            Components =
            {
                new ProductBundleComponent { Id = 1, ComponentProductId = 1, Qty = 2m, ComponentProduct = new Product { Id = 1 } },
                new ProductBundleComponent { Id = 2, ComponentProductId = 2, Qty = 1m, UnitWeightKg = 0.6m, ComponentProduct = new Product { Id = 2 } },
            },
        };
        var book = new BundlePriceBook();
        book.Set((1, null), 145m, 29m, false);
        book.Set((2, null), 100m, 40m, true);

        var pricing = BundlePricingEngine.Price(BundleService.BuildPricingInput(def, book, 1m, null));
        Assert.Equal(118m, pricing.RawPrice);     // 2 × 29 + 1 × (100 × 0.6)
        Assert.Equal(106.2m, pricing.UnitPrice);  // − 10%
    }
}
