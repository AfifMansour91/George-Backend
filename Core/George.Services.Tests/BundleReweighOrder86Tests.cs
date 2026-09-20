using George.DB;
using George.Services.Bundles;

namespace George.Services.Tests;

/// <summary>QA order #86 (store order, sum + re-weigh, two swaps - one with a ₪30 surcharge): the numbers of the picking screen.</summary>
public class BundleReweighOrder86Tests
{
    private static (OrderItem parent, List<OrderItem> children) Order86()
    {
        var parent = new OrderItem { Id = 1, ProductId = 13228, BundleProductId = 13228, Quantity = 1m, PickedQuantity = 1m, PricePerUnit = 179.30m, TotalPrice = 179.30m };
        var children = new List<OrderItem>
        {
            new() { Id = 2, ParentOrderItemId = 1, ProductId = 3824, Quantity = 0.2m, PricePerUnit = 300m, OrderLineQuantityMode = "weight", UnitWeightGrams = 1000m, SwappedFromProductId = 3825, SwapSurcharge = 30m },
            new() { Id = 3, ParentOrderItemId = 1, ProductId = 3802, Quantity = 2m, PricePerUnit = 29m, OrderLineQuantityMode = "units", UnitWeightGrams = 200m },
            new() { Id = 4, ParentOrderItemId = 1, ProductId = 3805, Quantity = 1m, PricePerUnit = 55.30m, OrderLineQuantityMode = "units", UnitWeightGrams = 700m, SwappedFromProductId = 3806, SwapSurcharge = 0m },
        };
        return (parent, children);
    }

    [Fact]
    public void WeighedAsOrdered_KeepsTheOrderedPrice_Overweight_RaisesItProportionally()
    {
        var (parent, children) = Order86();
        children[0].PickedQuantity = 0.2m; children[0].PickingUserConfirmed = true;
        children[1].PickedQuantity = 0.4m; children[1].PickingUserConfirmed = true;   // 2 pieces × 200 g, stored in kg
        children[2].PickedQuantity = 0.7m; children[2].PickingUserConfirmed = true;
        BundleOrderLineBuilder.ApplyPickingRules(parent, children, "sum", true, "none", 0m);
        Assert.Equal(179.30m, parent.TotalPrice);

        children[0].PickedQuantity = 0.22m;
        children[2].PickedQuantity = 0.75m;
        BundleOrderLineBuilder.ApplyPickingRules(parent, children, "sum", true, "none", 0m);
        // (149.30 / 173.30) × (66 + 58 + 59.25) + 30 - not the 213.25 a catalog re-price of the swapped-in products gave
        Assert.Equal(187.87m, parent.TotalPrice);
        Assert.Equal(parent.TotalPrice, children.Sum(c => c.TotalPrice));
    }

    [Fact]
    public void UnpickingEverything_ReturnsToTheOrderedPrice()
    {
        var (parent, children) = Order86();
        children[0].PickedQuantity = 0.3m; children[0].PickingUserConfirmed = true;
        BundleOrderLineBuilder.ApplyPickingRules(parent, children, "sum", true, "none", 0m);
        Assert.True(parent.TotalPrice > 179.30m);

        children[0].PickedQuantity = null; children[0].PickingUserConfirmed = false;
        BundleOrderLineBuilder.ApplyPickingRules(parent, children, "sum", true, "none", 0m);
        Assert.Equal(179.30m, parent.TotalPrice);
        Assert.Equal(179.30m, children.Sum(c => c.TotalPrice));
    }
}
