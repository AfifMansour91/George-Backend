using George.DB;
using George.Services;

namespace George.Services.Tests;

/// <summary>Bundles (מארזים) in דוח מוצרים - BUNDLES_SYNC_SPEC.md §8.</summary>
public class ProductsReportBundlesTests
{
    private static OrderItem Plain(int productId, decimal qty, decimal total) =>
        new() { ProductId = productId, Quantity = qty, TotalPrice = total };

    private static OrderItem Parent(int id, int bundleProductId, decimal bundles, decimal total, string? title = null) =>
        new() { Id = id, ProductId = bundleProductId, BundleProductId = bundleProductId, Quantity = bundles, PickedQuantity = bundles, TotalPrice = total, Title = title };

    private static OrderItem Child(int parentId, int productId, decimal qty) =>
        new() { ProductId = productId, ParentOrderItemId = parentId, Quantity = qty, TotalPrice = null };

    private static Product Prod(int id, string name, string? sku = null, string? setupType = null) => new()
    {
        Id = id,
        Name = name,
        Sku = sku,
        SetupType = setupType == null ? null : new SetupType { Name = setupType },
        ProductImage = new List<ProductImage> { new() { Url = $"https://img/{id}.jpg", SortOrder = 0 } },
    };

    [Fact]
    public void CountsTowardProductMetrics_OnlyPlainLines()
    {
        Assert.True(ProductsReportService.CountsTowardProductMetrics(Plain(1, 1m, 10m)));
        Assert.False(ProductsReportService.CountsTowardProductMetrics(Parent(5, 10, 1m, 199m)));
        Assert.False(ProductsReportService.CountsTowardProductMetrics(Child(5, 2293, 1m)));
    }

    [Fact]
    public void BuildBundlesSection_AggregatesParentLines_IgnoresChildrenAndPlainLines()
    {
        var products = new Dictionary<int, Product>
        {
            [10] = Prod(10, "מארז על האש", "BBQ-01", "bundle"),
            [11] = Prod(11, "מארז משפחתי", "FAM-01", "bundle"),
            [2293] = Prod(2293, "אנטרקוט"),
        };
        var orders = new List<Order>
        {
            new()
            {
                Id = 1,
                OrderItem = new List<OrderItem>
                {
                    Plain(2293, 2m, 300m),
                    Parent(100, 10, 2m, 400m),
                    Child(100, 2293, 2m),
                },
            },
            new()
            {
                Id = 2,
                OrderItem = new List<OrderItem>
                {
                    Parent(200, 10, 1m, 200m),
                    Child(200, 2293, 1m),
                    Parent(201, 11, 1m, 100m),
                },
            },
            new() { Id = 3, OrderItem = new List<OrderItem> { Plain(2293, 1m, 150m) } },
        };

        var section = ProductsReportService.BuildBundlesSection(orders, products);

        Assert.Equal(2, section.OrdersCount);
        Assert.Equal(4m, section.UnitsSold);
        Assert.Equal(700m, section.Revenue);
        Assert.Equal(2, section.Rows.Count);

        var top = section.Rows[0];
        Assert.Equal(10, top.ProductId);
        Assert.Equal("מארז על האש", top.Name);
        Assert.Equal("BBQ-01", top.Sku);
        Assert.Equal("https://img/10.jpg", top.ImageUrl);
        Assert.Equal(2, top.OrdersCount);
        Assert.Equal(3m, top.UnitsSold);
        Assert.Equal(600m, top.Revenue);
        Assert.Equal(0.8571m, top.Share);

        var second = section.Rows[1];
        Assert.Equal(11, second.ProductId);
        Assert.Equal(1, second.OrdersCount);
        Assert.Equal(100m, second.Revenue);
        Assert.Equal(0.1429m, second.Share);
    }

    [Fact]
    public void BuildBundlesSection_UnknownBundleProduct_FallsBackToLineTitle()
    {
        var orders = new List<Order>
        {
            new() { Id = 1, OrderItem = new List<OrderItem> { Parent(1, 77, 1m, 50m, "מארז שנמחק") } },
        };

        var section = ProductsReportService.BuildBundlesSection(orders, new Dictionary<int, Product>());

        var row = Assert.Single(section.Rows);
        Assert.Equal("מארז שנמחק", row.Name);
        Assert.Null(row.ImageUrl);
        Assert.Equal(1m, row.Share);
    }

    [Fact]
    public void BuildBundlesSection_CategoryFilter_AppliesToBundleProduct()
    {
        var bundle = Prod(10, "מארז", "B", "bundle");
        bundle.ProductCategory = new List<ProductCategory> { new() { CategoryId = 5, IsPrimary = true } };
        var products = new Dictionary<int, Product> { [10] = bundle };
        var orders = new List<Order>
        {
            new() { Id = 1, OrderItem = new List<OrderItem> { Parent(1, 10, 1m, 50m) } },
        };

        Assert.Single(ProductsReportService.BuildBundlesSection(orders, products, categoryId: 5).Rows);
        Assert.Empty(ProductsReportService.BuildBundlesSection(orders, products, categoryId: 6).Rows);
    }
}
