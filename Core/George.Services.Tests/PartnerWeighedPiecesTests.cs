using George.Services.Partner;
using George.Services.Response;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Partner API, weighed products ordered by the piece (Zano "3 דניס בינוניים", 2026-10-19). Before this a by_unit +
/// IsWeighted product was exposed as soldBy "weight" (quantity in kg) and priced ₪/kg × pieces; now it is a unit
/// product priced per kg with an approx piece weight, estimated as pieces × kg × ₪/kg and re-weighed at picking.
/// </summary>
public class PartnerWeighedPiecesTests
{
    /// <summary>Zano product 11941 "דניס": by_unit, weighed, weight by variant (0.5 kg "בינוני"), 140 ₪/kg on the variant.</summary>
    private static ProductRes Denis() => new()
    {
        Id = 11941,
        Name = "דניס",
        Price = 90m,
        IsWeighted = true,
        SetupType = "by_unit",
        WeightUnit = "kg",
        WeightConfig = new WeightConfigRes { Unit = "kg", WeightByVariant = true, UnitWeightMode = "by_variant" },
        Variants = new List<ProductVariantRes>
        {
            new() { Id = 501, Price = 140m, Weight = 0.5m, OptionValues = new Dictionary<string, string> { ["גודל"] = "בינוני", ["חיתוך"] = "פילה" } },
            new() { Id = 502, Price = 90m, Weight = 0.9m, OptionValues = new Dictionary<string, string> { ["גודל"] = "גדול", ["חיתוך"] = "שלם" } },
        },
    };

    [Fact]
    public void Weighed_by_unit_product_is_sold_by_unit_and_priced_per_kg_with_piece_weights_on_variants()
    {
        var p = PartnerService.MapProductToPartner(Denis());
        Assert.Equal("unit", p.SoldBy);
        Assert.True(p.PricePerKg);
        Assert.True(p.RequiresVariant);
        Assert.Null(p.ApproxUnitWeightGrams); // depends on the size variant
        Assert.Equal(500m, p.Variants.Single(v => v.Id == 501).ApproxUnitWeightGrams);
        Assert.Equal(900m, p.Variants.Single(v => v.Id == 502).ApproxUnitWeightGrams);
    }

    [Fact]
    public void Loose_weight_product_stays_sold_by_weight()
    {
        var minced = new ProductRes { Id = 1, Name = "טחון", Price = 79m, IsWeighted = true, SetupType = "by_weight", WeightUnit = "kg" };
        var p = PartnerService.MapProductToPartner(minced);
        Assert.Equal("weight", p.SoldBy);
        Assert.True(p.PricePerKg);
        Assert.Null(p.ApproxUnitWeightGrams);
    }

    [Fact]
    public void Plain_unit_product_is_not_priced_per_kg()
    {
        var sauce = new ProductRes { Id = 2, Name = "רוטב", Price = 12m, IsWeighted = false, SetupType = "standard" };
        var p = PartnerService.MapProductToPartner(sauce);
        Assert.Equal("unit", p.SoldBy);
        Assert.False(p.PricePerKg);
    }

    [Fact]
    public void Three_medium_fish_are_estimated_as_pieces_times_weight_times_price_per_kg()
    {
        var est = PartnerService.EstimateWeighedPieces(pieces: 3m, approxUnitWeightGrams: 500m, pricePerKg: 140m);
        Assert.NotNull(est);
        Assert.Equal(1.5m, est!.Value.EstimatedKg);
        Assert.Equal(210m, est.Value.LineTotal);
        Assert.Null(PartnerService.EstimateWeighedPieces(3m, null, 140m)); // no catalog weight → caller falls back
    }

    [Fact]
    public void Line_labels_match_the_manual_order_screen()
    {
        // Under a kilo the manual-order screen writes grams, from a kilo up it writes kg (Dubi Dagim "5.5 ק"ג ליח'").
        Assert.Equal("500 גרם ליח'", PartnerService.FormatPerUnitWeightLabel(500m));
        Assert.Equal("5.5 ק\"ג ליח'", PartnerService.FormatPerUnitWeightLabel(5500m));
        Assert.Equal("בינוני / פילה (כ 500 גרם)", PartnerService.FormatSizeWithApproxWeight("בינוני / פילה", 500m));
        Assert.Equal("בין 5-6 ק\"ג (כ 5.5 ק\"ג)", PartnerService.FormatSizeWithApproxWeight("בין 5-6 ק\"ג", 5500m));
    }
}
