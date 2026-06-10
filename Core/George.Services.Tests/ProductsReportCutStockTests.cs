using George.DB;
using George.Services;

namespace George.Services.Tests;

public class ProductsReportCutStockTests
{
    [Fact]
    public void ClassifyCutRowStock_UsesVariantWhenIdProvided()
    {
        var p = new Product
        {
            StockManagementType = new StockManagementType { Name = "variation" },
            VariationStockByQuantity = true,
            LowStockThreshold = 5m,
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 10,
                    IsDeleted = false,
                    StockQuantity = 2m,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "גודל", OptionValue = "קטן" },
                    },
                },
                new()
                {
                    Id = 11,
                    IsDeleted = false,
                    StockQuantity = 20m,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "גודל", OptionValue = "גדול" },
                    },
                },
            },
        };

        Assert.Equal("low", ProductCatalogStockClassification.ClassifyCutRowStock(p, null, 10));
        Assert.Equal("ok", ProductCatalogStockClassification.ClassifyCutRowStock(p, null, 11));
    }

    [Fact]
    public void FindVariantForOrderLine_MatchesVariantTitleOnOrderItem()
    {
        var p = new Product
        {
            Name = "סטייק",
            StockManagementType = new StockManagementType { Name = "variation" },
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 42,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "עובי", OptionValue = "אצבע" },
                    },
                },
            },
        };
        var line = new OrderItem { VariantTitle = "עובי אצבע (כ 200 גר')" };

        var v = ProductCatalogVariantResolution.FindVariantForOrderLine(p, line);

        Assert.NotNull(v);
        Assert.Equal(42, v!.Id);
    }
}
