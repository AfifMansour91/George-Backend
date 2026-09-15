using George.DB;
using George.Services;

namespace George.Services.Tests;

/// <summary>Bundles (מארזים) in דוח ריכוז כמויות - BUNDLES_SYNC_SPEC.md §8.</summary>
public class QuantityConcentrationReportBundlesTests
{
    private static Product Weighted(int id, string name) => new() { Id = id, Name = name, IsWeighted = true };
    private static Product Units(int id, string name) => new() { Id = id, Name = name, IsWeighted = false };

    private static OrderItem Parent(int id, int bundleProductId, decimal bundles, bool confirmed = false) =>
        new() { Id = id, ProductId = bundleProductId, BundleProductId = bundleProductId, Quantity = bundles, PickedQuantity = bundles, PickingUserConfirmed = confirmed };

    private static OrderItem Child(int parentId, int productId, decimal qty, string? mode = null, bool confirmed = false, decimal? picked = null) =>
        new()
        {
            ProductId = productId,
            ParentOrderItemId = parentId,
            Quantity = qty,
            OrderLineQuantityMode = mode,
            PickingUserConfirmed = confirmed,
            PickedQuantity = picked,
        };

    [Fact]
    public void SplitBundleChildQty_WeightMode_QuantityIsKg()
    {
        var (kg, units) = QuantityConcentrationReportService.SplitBundleChildQty(Child(1, 2293, 1.5m, "weight"), Weighted(2293, "אנטרקוט"));
        Assert.Equal(1.5m, kg);
        Assert.Equal(0m, units);
    }

    [Fact]
    public void SplitBundleChildQty_WeightedProductNoMode_QuantityIsKg()
    {
        var (kg, units) = QuantityConcentrationReportService.SplitBundleChildQty(Child(1, 2293, 2m), Weighted(2293, "אנטרקוט"));
        Assert.Equal(2m, kg);
        Assert.Equal(0m, units);
    }

    [Fact]
    public void SplitBundleChildQty_UnitsProduct_QuantityIsUnits_PickedWinsWhenConfirmed()
    {
        var line = Child(1, 50, 4m, "units", confirmed: true, picked: 3m);
        var (kg, units) = QuantityConcentrationReportService.SplitBundleChildQty(line, Units(50, "פיתות"));
        Assert.Equal(0m, kg);
        Assert.Equal(3m, units);

        var unconfirmed = Child(1, 50, 4m, "units", confirmed: false, picked: 3m);
        Assert.Equal(4m, QuantityConcentrationReportService.SplitBundleChildQty(unconfirmed, Units(50, "פיתות")).units);
    }

    [Fact]
    public void SplitLineQtyForReport_PlainLine_KeepsExistingRules()
    {
        var plain = new OrderItem { ProductId = 1, OrderLineQuantityMode = "weight", Quantity = 1m, SaleTotalWeight = "2.5" };
        var (kg, units) = QuantityConcentrationReportService.SplitLineQtyForReport(plain, Weighted(1, "נתח"));
        Assert.Equal(2.5m, kg);
        Assert.Equal(0m, units);
    }

    [Fact]
    public void IsBundleParentPicked_ParentConfirmedOrAllChildrenConfirmed()
    {
        var parent = Parent(1, 10, 1m);
        Assert.False(QuantityConcentrationReportService.IsBundleParentPicked(parent, Array.Empty<OrderItem>()));
        Assert.False(QuantityConcentrationReportService.IsBundleParentPicked(parent, new[] { Child(1, 2, 1m, confirmed: true), Child(1, 3, 1m) }));
        Assert.True(QuantityConcentrationReportService.IsBundleParentPicked(parent, new[] { Child(1, 2, 1m, confirmed: true), Child(1, 3, 1m, confirmed: true) }));
        Assert.True(QuantityConcentrationReportService.IsBundleParentPicked(Parent(2, 10, 1m, confirmed: true), Array.Empty<OrderItem>()));
    }

    [Fact]
    public void BuildBundlesSection_AggregatesParentsAndComponentDemand()
    {
        var products = new Dictionary<int, Product>
        {
            [10] = new() { Id = 10, Name = "מארז על האש" },
            [2293] = Weighted(2293, "אנטרקוט"),
            [50] = Units(50, "פיתות"),
        };
        var orders = new List<Order>
        {
            new()
            {
                Id = 1,
                OrderItem = new List<OrderItem>
                {
                    Parent(100, 10, 2m),
                    Child(100, 2293, 2m, "weight", confirmed: true, picked: 2.2m),
                    Child(100, 50, 12m, "units", confirmed: true, picked: 12m),
                },
            },
            new()
            {
                Id = 2,
                OrderItem = new List<OrderItem>
                {
                    Parent(200, 10, 1m),
                    Child(200, 2293, 1m, "weight"),
                    Child(200, 50, 6m, "units"),
                    new OrderItem { ProductId = 2293, Quantity = 1m, OrderLineQuantityMode = "weight" }, // plain line - not a component
                },
            },
        };

        var section = QuantityConcentrationReportService.BuildBundlesSection(orders, products, categoryFilter: null);

        var b = Assert.Single(section);
        Assert.Equal(10, b.BundleProductId);
        Assert.Equal("מארז על האש", b.Name);
        Assert.Equal(3m, b.OrderedBundles);
        Assert.Equal(2m, b.PickedBundles);   // order 1: all children confirmed; order 2: none
        Assert.Equal(2, b.OrdersCount);

        Assert.Equal(2, b.Components.Count);
        var ent = b.Components.Single(c => c.ProductId == 2293);
        Assert.Equal(3.2m, ent.QuantityKg);  // 2.2 picked + 1 ordered
        Assert.Equal(0m, ent.QuantityUnits);
        var pita = b.Components.Single(c => c.ProductId == 50);
        Assert.Equal(0m, pita.QuantityKg);
        Assert.Equal(18m, pita.QuantityUnits);
    }

    [Fact]
    public void BuildBundlesSection_NoParents_Empty()
    {
        var orders = new List<Order>
        {
            new() { Id = 1, OrderItem = new List<OrderItem> { new() { ProductId = 1, Quantity = 1m } } },
        };
        Assert.Empty(QuantityConcentrationReportService.BuildBundlesSection(orders, new Dictionary<int, Product>(), null));
    }
}
