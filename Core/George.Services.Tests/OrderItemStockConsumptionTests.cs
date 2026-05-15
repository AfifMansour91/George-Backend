using George.Data;
using George.DB;

namespace George.Services.Tests;

public class OrderItemStockConsumptionTests
{
    [Fact]
    public void ResolveOrdered_UnitsWith200gPerSlice_UsesKgTotal()
    {
        var line = new OrderItem
        {
            Quantity = 10m,
            OrderLineQuantityMode = "units",
            UnitWeightGrams = 200m,
        };
        Assert.Equal(2m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(line));
    }

    [Fact]
    public void ResolveOrdered_WeightMode_PrefersTotalPricePerPricePerUnitKg()
    {
        var line = new OrderItem
        {
            Quantity = 1m,
            OrderLineQuantityMode = "weight",
            TotalPrice = 626.50m,
            PricePerUnit = 179m,
        };
        Assert.Equal(3.5m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(line));
    }

    [Fact]
    public void ResolveOrdered_WeightMode_ByWeightGramsCart_UsesUnitWeightGramsAsTotalKg()
    {
        var line = new OrderItem
        {
            Quantity = 1m,
            OrderLineQuantityMode = "weight",
            UnitWeightGrams = 300m,
        };
        Assert.Equal(0.3m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(line));
    }

    [Fact]
    public void ResolveOrdered_WeightMode_FallsBackToQuantityWhenNoLineEconomics()
    {
        var line = new OrderItem
        {
            Quantity = 2.5m,
            OrderLineQuantityMode = "weight",
            UnitWeightGrams = 1000m,
        };
        Assert.Equal(2.5m, OrderItemStockConsumption.ResolveOrderedCatalogConsumption(line));
    }

    [Fact]
    public void ResolvePicking_KgUi_DeltaNotScaled()
    {
        var line = new OrderItem
        {
            OrderLineQuantityMode = "units",
            UnitWeightGrams = 200m,
        };
        Assert.Equal(0.2m, OrderItemStockConsumption.ResolvePickingDeltaCatalogConsumption(line, 2m, 2.2m));
    }

    [Fact]
    public void ResolvePicking_PieceUi_ScaledByUnitKg()
    {
        var line = new OrderItem
        {
            OrderLineQuantityMode = "units",
            UnitWeightGrams = null,
            LineUnitWeightKg = 0.4m,
        };
        Assert.Equal(0.4m, OrderItemStockConsumption.ResolvePickingDeltaCatalogConsumption(line, 0m, 1m));
    }
}
