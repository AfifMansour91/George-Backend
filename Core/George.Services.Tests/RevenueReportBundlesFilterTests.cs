using George.DB;
using George.Services;

namespace George.Services.Tests;

/// <summary>דוח הכנסות - "הכנסות ממארזים" filter and money lines (BUNDLES_SYNC_SPEC.md §8).</summary>
public class RevenueReportBundlesFilterTests
{
    private static Order OrderWithBundle() => new()
    {
        Id = 1,
        OrderItem = new List<OrderItem>
        {
            new() { Id = 1, ProductId = 5, Quantity = 1m, TotalPrice = 100m },
            new() { Id = 2, ProductId = 10, BundleProductId = 10, Quantity = 1m, PickedQuantity = 1m, TotalPrice = 300m },
            // component lines: informational shares / catalog prices - never money of their own
            new() { Id = 3, ProductId = 6, ParentOrderItemId = 2, Quantity = 1m, TotalPrice = 200m },
            new() { Id = 4, ProductId = 7, ParentOrderItemId = 2, Quantity = 2m, PricePerUnit = 50m },
        },
    };

    [Fact]
    public void BundlesOnly_ShareIsTheBundleParentsPartOfTheMoneyLines()
    {
        var filter = new RevenueReportService.RevenueLineFilter(new HashSet<int>(), bundlesOnly: true);
        Assert.Equal(0.75m, RevenueReportService.CategoryFilterShare(OrderWithBundle(), new Dictionary<int, Product>(), filter));
    }

    [Fact]
    public void NoFilter_FullOrder_And_OrderWithoutBundles_HasNoBundleShare()
    {
        var none = new RevenueReportService.RevenueLineFilter(new HashSet<int>(), bundlesOnly: false);
        Assert.Equal(1m, RevenueReportService.CategoryFilterShare(OrderWithBundle(), new Dictionary<int, Product>(), none));

        var plainOnly = new Order { Id = 2, OrderItem = new List<OrderItem> { new() { Id = 9, ProductId = 5, Quantity = 1m, TotalPrice = 80m } } };
        var bundles = new RevenueReportService.RevenueLineFilter(new HashSet<int>(), bundlesOnly: true);
        Assert.Equal(0m, RevenueReportService.CategoryFilterShare(plainOnly, new Dictionary<int, Product>(), bundles));
    }
}
