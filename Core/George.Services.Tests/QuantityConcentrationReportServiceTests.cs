using George.DB;
using George.Services;
using George.Services.Response;

namespace George.Services.Tests;

public class QuantityConcentrationReportServiceTests
{
    private static Product WeightedProduct() => new() { Id = 1, Name = "נתח", IsWeighted = true };

    [Fact]
    public void SplitLineQty_WeightMode_IgnoresLineUnit()
    {
        var line = new OrderItem
        {
            OrderLineQuantityMode = "weight",
            Quantity = 1m,
            SaleTotalWeight = "2.5",
            LineUnit = 1m,
        };
        var p = WeightedProduct();
        var (kg, units) = QuantityConcentrationReportService.SplitLineQty(line, p);
        Assert.Equal(2.5m, kg);
        Assert.Equal(0m, units);
    }

    [Fact]
    public void SplitLineQty_WeightedSoldByUnits_KeepsUnitsAndKg()
    {
        var line = new OrderItem
        {
            OrderLineQuantityMode = "units",
            Quantity = 3m,
            UnitWeightGrams = 250m,
            LineUnit = 3m,
        };
        var p = WeightedProduct();
        var (kg, units) = QuantityConcentrationReportService.SplitLineQty(line, p);
        Assert.Equal(0.75m, kg);
        Assert.Equal(3m, units);
    }

    [Fact]
    public void CollapseIfSingleSyntheticLine_RemovesDuplicateProductNameRow()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "בשר טחון",
                QuantityKg = 15m,
                QuantityUnits = null,
                WeightPerUnitKg = null,
                Note = null,
            },
        };
        var result = QuantityConcentrationReportService.CollapseIfSingleSyntheticLine(lines, "בשר טחון");
        Assert.Empty(result);
    }

    [Fact]
    public void BuildLineLabel_PrefersCutLabelOverSaleUnitsLine()
    {
        var line = new OrderItem
        {
            OrderLineCuttingLabel = "עובי אצבע (כ 200 גר')",
            SaleUnitsLine = "עובי אצבע (כ 200 גר') 200 גר') 200 גר')",
        };
        var label = QuantityConcentrationReportService.BuildLineLabel(line);
        Assert.Equal("עובי אצבע (כ 200 גר')", label);
    }

    [Fact]
    public void BuildLineLabel_PrefersVariantTitle_OverComputedCutAndSize()
    {
        var line = new OrderItem
        {
            VariantTitle = "עובי אצבע (כ 200 גר')",
            OrderLineCuttingLabel = "עובי אצבע",
            OrderLineSizeLabel = "(כ 200 גרם)",
        };
        var label = QuantityConcentrationReportService.BuildLineLabel(line, "סטייק סינטה");
        Assert.Equal("עובי אצבע (כ 200 גר')", label);
    }

    [Fact]
    public void ApplyDetailLineDisplayRules_SimpleProduct_UsesProductName_NotWeightLabels()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "500 גרם ליח'",
                QuantityKg = 0.5m,
                QuantityUnits = 1m,
                WeightPerUnitKg = 0.5m,
            },
            new()
            {
                LineLabel = "3 יח'",
                QuantityKg = 0.75m,
                QuantityUnits = 3m,
            },
        };
        var p = new Product { Id = 1, Name = "נתח בקר", IsWeighted = true };
        var result = QuantityConcentrationReportService.ApplyDetailLineDisplayRules(lines, p);
        Assert.Single(result);
        Assert.Equal("נתח בקר", result[0].LineLabel);
        Assert.Equal(1.25m, result[0].QuantityKg);
        Assert.Equal(4m, result[0].QuantityUnits);
    }

    [Fact]
    public void ApplyDetailLineDisplayRules_SimpleProduct_SplitsRowsByNote_WithProductName()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "500 גרם ליח'",
                QuantityKg = 1m,
                QuantityUnits = 2m,
                Note = "ללא שומן",
            },
            new()
            {
                LineLabel = "3 יח'",
                QuantityKg = 0.5m,
                QuantityUnits = 1m,
                Note = null,
            },
        };
        var p = new Product { Id = 1, Name = "פלאט איירון", IsWeighted = true };
        var result = QuantityConcentrationReportService.ApplyDetailLineDisplayRules(lines, p);
        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("פלאט איירון", r.LineLabel));
        Assert.Contains(result, r => r.Note == "ללא שומן" && r.QuantityKg == 1m);
        Assert.Contains(result, r => r.Note == null && r.QuantityUnits == 1m);
    }

    [Fact]
    public void AppendRemainderDetailLineIfNeeded_SkipsForSimpleProductWithoutVariations()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "נתח שלם",
                QuantityKg = 2m,
                QuantityUnits = 1m,
            },
        };
        var p = new Product { Id = 1, Name = "נתח שלם", IsWeighted = true };
        var result = QuantityConcentrationReportService.AppendRemainderDetailLineIfNeeded(lines, 3m, 1m, p);
        Assert.Single(result);
    }

    [Fact]
    public void ApplyDetailLineDisplayRules_WeightedWithCatalogVariants_KeepsVariantLabels_EvenWhenStockIsQuantity()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new() { LineLabel = "עובי אצבע (כ 200 גר')", QuantityKg = 1m },
            new() { LineLabel = "נתח שלם", QuantityKg = 2m },
        };
        var p = new Product
        {
            Id = 1,
            Name = "סטייק אנטריקוט",
            IsWeighted = true,
            StockManagementType = new StockManagementType { Name = "quantity" },
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 1,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionValue = "עובי אצבע (כ 200 גר')" },
                    },
                },
                new()
                {
                    Id = 2,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionValue = "נתח שלם" },
                    },
                },
            },
        };
        var result = QuantityConcentrationReportService.ApplyDetailLineDisplayRules(lines, p);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.LineLabel == "עובי אצבע (כ 200 גר')");
        Assert.Contains(result, r => r.LineLabel == "נתח שלם");
    }

    [Fact]
    public void ApplyDetailLineDisplayRules_MergesLinesWithSameNormalizedLabel()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "עובי אצבע (כ 200 גר')",
                QuantityKg = 0.2m,
                QuantityUnits = null,
                WeightPerUnitKg = null,
                Note = null,
            },
            new()
            {
                LineLabel = "עובי אצבע (כ 200 גר')",
                QuantityKg = 2m,
                QuantityUnits = 10m,
                WeightPerUnitKg = 0.2m,
                Note = null,
            },
        };
        var p = new Product { Id = 2, Name = "סטייק סינטה", IsWeighted = true };
        var result = QuantityConcentrationReportService.ApplyDetailLineDisplayRules(lines, p);
        Assert.Single(result);
        Assert.Equal(2.2m, result[0].QuantityKg);
        Assert.Equal(10m, result[0].QuantityUnits);
        Assert.Equal(0.2m, result[0].WeightPerUnitKg);
    }

    [Fact]
    public void AppendRemainderDetailLineIfNeeded_AddsUnattributedRowWhenTotalsExceedDisplayedDetail()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "עובי אצבע",
                QuantityKg = 2m,
                QuantityUnits = 1m,
                WeightPerUnitKg = 2m,
                Note = null,
            },
        };
        var p = new Product
        {
            Id = 1,
            Name = "סטייק",
            IsWeighted = true,
            StockManagementType = new StockManagementType { Name = "variation" },
            ProductVariant = new List<ProductVariant> { new() { Id = 1, IsDeleted = false } },
        };
        var withRemainder = QuantityConcentrationReportService.AppendRemainderDetailLineIfNeeded(lines, 3m, 1m, p);
        Assert.Equal(2, withRemainder.Count);
        Assert.Equal(QuantityConcentrationReportService.RemainderLineLabel, withRemainder[1].LineLabel);
        Assert.Equal(1m, withRemainder[1].QuantityKg);
        Assert.Null(withRemainder[1].QuantityUnits);
        Assert.Null(withRemainder[1].WeightPerUnitKg);
    }

    [Fact]
    public void AppendRemainderDetailLineIfNeeded_SkipsWhenNoDetailRowsSoParentRowIsEnough()
    {
        var unchanged = QuantityConcentrationReportService.AppendRemainderDetailLineIfNeeded(
            new List<QuantityConcentrationLineDto>(),
            2m,
            0m);
        Assert.Empty(unchanged);
    }
}
