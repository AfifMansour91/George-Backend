using System.Text.Json;
using George.DB;
using George.Services;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// WooCommerce import: OCWSU weight units meta (grams vs kg) must map to George Unit "g" / "kg".
/// </summary>
public class WooCommerceImportWeightUnitsTests
{
    private static readonly List<Unit> DefaultUnits = new()
    {
        new Unit { Id = 1, Name = "kg", IsDeleted = false },
        new Unit { Id = 2, Name = "g", IsDeleted = false },
    };

    [Theory]
    [InlineData("grams", 2)]
    [InlineData("Grams", 2)]
    [InlineData("g", 2)]
    [InlineData("kg", 1)]
    [InlineData("KG", 1)]
    public void ResolveUnitIdFromOcwsu_maps_meta_to_george_unit(string ocwsuUnits, int expectedUnitId)
    {
        var id = WooCommerceImportUnitMapping.ResolveUnitIdFromOcwsu(DefaultUnits, ocwsuUnits);
        Assert.Equal(expectedUnitId, id);
    }

    [Fact]
    public void ResolveUnitIdFromOcwsu_matches_hebrew_gram_unit_name()
    {
        var units = new List<Unit>
        {
            new() { Id = 10, Name = "ק\"ג", IsDeleted = false },
            new() { Id = 11, Name = "גרם", IsDeleted = false },
        };
        Assert.Equal(11, WooCommerceImportUnitMapping.ResolveUnitIdFromOcwsu(units, "grams"));
    }

    [Fact]
    public void Deserialize_salmon_product_meta_includes_grams_units()
    {
        var jsonPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "..", "shop-manager", "woo-product-3504.json");
        if (!File.Exists(jsonPath))
        {
            jsonPath = @"C:\Users\user\Documents\Projects\ShopManager\shop-manager\woo-product-3504.json";
        }
        Assert.True(File.Exists(jsonPath), $"Fixture not found: {jsonPath}");

        var json = File.ReadAllText(jsonPath);
        using var doc = JsonDocument.Parse(json);
        var meta = doc.RootElement.GetProperty("meta_data");
        string? ocwsuUnits = null;
        foreach (var entry in meta.EnumerateArray())
        {
            if (entry.TryGetProperty("key", out var keyEl)
                && keyEl.GetString() == "_ocwsu_product_weight_units"
                && entry.TryGetProperty("value", out var valEl))
            {
                ocwsuUnits = valEl.GetString();
                break;
            }
        }

        Assert.Equal("grams", ocwsuUnits);
        Assert.Equal(2, WooCommerceImportUnitMapping.ResolveUnitIdFromOcwsu(DefaultUnits, ocwsuUnits));
    }
}
