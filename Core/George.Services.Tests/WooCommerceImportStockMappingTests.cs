using George.Services;
using Xunit;

namespace George.Services.Tests;

public class WooCommerceImportStockMappingTests
{
  private static WooCommerceImportStockMapping.VariationStockSource V(
        bool? manageStock,
        decimal? qty = null,
        string? status = "instock") =>
        new(manageStock, qty, status);

    [Fact]
    public void Variable_product_with_uniform_instock_and_no_manage_stock_uses_parent_status_mode()
    {
        var variations = new[]
        {
            V(false, 1, "instock"),
            V(false, 1, "instock"),
        };

        Assert.False(WooCommerceImportStockMapping.UsesVariationStockManagement(variations));
        Assert.Equal("status", WooCommerceImportStockMapping.ResolveStockManagementTypeName(false, true, variations));
        Assert.False(WooCommerceImportStockMapping.ResolveVariationStockByQuantity(variations));
    }

    [Fact]
    public void Variable_product_with_differing_stock_status_uses_variation_mode_binary()
    {
        var variations = new[]
        {
            V(false, null, "instock"),
            V(false, null, "outofstock"),
        };

        Assert.True(WooCommerceImportStockMapping.UsesVariationStockManagement(variations));
        Assert.Equal("variation", WooCommerceImportStockMapping.ResolveStockManagementTypeName(false, true, variations));
        Assert.False(WooCommerceImportStockMapping.VariationTracksQuantity(variations));
        Assert.Equal(1m, WooCommerceImportStockMapping.ResolveVariantStockQuantity(variations[0], usesVariationStock: true));
        Assert.Equal(0m, WooCommerceImportStockMapping.ResolveVariantStockQuantity(variations[1], usesVariationStock: true));
    }

    [Fact]
    public void Variation_with_manage_stock_uses_quantity_mode_and_real_qty()
    {
        var variations = new[] { V(true, 12m, "instock") };

        Assert.True(WooCommerceImportStockMapping.VariationTracksQuantity(variations));
        Assert.True(WooCommerceImportStockMapping.ResolveVariationStockByQuantity(variations));
        Assert.Equal(12m, WooCommerceImportStockMapping.ResolveVariantStockQuantity(variations[0], usesVariationStock: true));
    }

    [Fact]
    public void Spurious_stock_quantity_without_manage_stock_does_not_enable_quantity_mode()
    {
        var variations = new[] { V(false, 1m, "instock") };

        Assert.False(WooCommerceImportStockMapping.VariationTracksQuantity(variations));
        Assert.False(WooCommerceImportStockMapping.ResolveVariationStockByQuantity(variations));
    }

    [Fact]
    public void Parent_manage_stock_maps_to_quantity_mode()
    {
        Assert.Equal("quantity", WooCommerceImportStockMapping.ResolveStockManagementTypeName(true, true, Array.Empty<WooCommerceImportStockMapping.VariationStockSource>()));
    }

    [Fact]
    public void Variant_stock_quantity_null_when_not_using_variation_stock()
    {
        var variation = V(false, null, "instock");
        Assert.Null(WooCommerceImportStockMapping.ResolveVariantStockQuantity(variation, usesVariationStock: false));
    }
}
