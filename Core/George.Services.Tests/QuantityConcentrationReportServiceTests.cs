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
    public void ApplyDetailLineDisplayRules_SimpleProduct_SplitsRowsByNote_WithNoteBucketWeight()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "500 גרם ליח'",
                QuantityKg = 1m,
                QuantityUnits = 2m,
                WeightPerUnitKg = 0.5m,
                Note = "ללא שומן",
            },
            new()
            {
                LineLabel = "3 יח'",
                QuantityKg = 0.5m,
                QuantityUnits = 1m,
                WeightPerUnitKg = 0.5m,
                Note = null,
            },
        };
        var p = new Product { Id = 1, Name = "פלאט איירון", IsWeighted = true };
        var result = QuantityConcentrationReportService.ApplyDetailLineDisplayRules(lines, p);
        Assert.Equal(2, result.Count);
        Assert.All(result, r =>
        {
            Assert.Equal("noteBucket", r.LineDisplayKind);
            Assert.Equal("", r.LineLabel);
            Assert.Equal(0.5m, r.WeightPerUnitKg);
        });
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
        Assert.Contains(result, r => r.LineLabel == "עובי אצבע" && r.LineDisplayKind == "variant");
        Assert.Contains(result, r => r.LineLabel == "נתח שלם" && r.LineDisplayKind == "variant");
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
        Assert.Equal("weightChoice", result[0].LineDisplayKind);
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
        Assert.Equal("remainder", withRemainder[1].LineDisplayKind);
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

    /// <summary>GDBEEF: catalog options NAMED as weights ("500 גרם", "1 ק"ג") must still show as variant rows.</summary>
    [Fact]
    public void ApplyDetailLineDisplayRules_WeightedProduct_KeepsWeightNamedVariantLines()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new() { LineLabel = "750 גרם", VariantId = 15566, QuantityKg = 1.5m, QuantityUnits = 2m },
            new() { LineLabel = "1 ק\"ג", VariantId = 15564, QuantityKg = 1m, QuantityUnits = 1m },
        };
        var p = new Product
        {
            Id = 7243,
            Name = "אנטריקוט על עצם",
            IsWeighted = true,
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 15566,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "צורת חיתוך", OptionValue = "750 גרם" },
                    },
                },
                new()
                {
                    Id = 15564,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "צורת חיתוך", OptionValue = "1 ק\"ג" },
                    },
                },
            },
        };
        var result = QuantityConcentrationReportService.ApplyDetailLineDisplayRules(lines, p);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.LineLabel == "750 גרם" && r.LineDisplayKind == "variant");
        Assert.Contains(result, r => r.LineLabel == "1 ק\"ג" && r.LineDisplayKind == "variant");
    }

    /// <summary>Single weight-named variant line must survive the single-synthetic-line collapse.</summary>
    [Fact]
    public void CollapseIfSingleSyntheticLine_KeepsSingleWeightNamedVariantLine()
    {
        var lines = new List<QuantityConcentrationLineDto>
        {
            new() { LineLabel = "500 גרם", VariantId = 15565, QuantityKg = 0.5m },
        };
        var result = QuantityConcentrationReportService.CollapseIfSingleSyntheticLine(lines, "אנטריקוט על עצם");
        Assert.Single(result);
    }

    [Fact]
    public void FindVariantForOrderLine_MatchesWeightNamedOptionByRawVariantTitle()
    {
        var p = new Product
        {
            Id = 7243,
            Name = "אנטריקוט על עצם",
            IsWeighted = true,
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 15565,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "צורת חיתוך", OptionValue = "500 גרם" },
                    },
                },
            },
        };
        // No ProductVariantId / Woo id on the line — only the weight-looking title.
        var line = new OrderItem { VariantTitle = "500 גרם", OrderLineSizeLabel = "(כ 500 גרם)" };
        var v = ProductCatalogVariantResolution.FindVariantForOrderLine(p, line);
        Assert.NotNull(v);
        Assert.Equal(15565, v!.Id);
    }

    /// <summary>Stale ProductVariantId (variants re-created since the order) must fall through to Woo-id matching.</summary>
    [Fact]
    public void FindVariantForOrderLine_StaleProductVariantId_FallsBackToWooVariationId()
    {
        var p = new Product
        {
            Id = 7243,
            Name = "אנטריקוט על עצם",
            IsWeighted = true,
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 15566,
                    IsDeleted = false,
                    WooCommerceVariationId = 16135,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "צורת חיתוך", OptionValue = "750 גרם" },
                    },
                },
            },
        };
        var line = new OrderItem
        {
            ProductVariantId = 7009,
            WooCommerceVariationId = 16135,
            VariantTitle = "750 גרם",
        };
        var v = ProductCatalogVariantResolution.FindVariantForOrderLine(p, line);
        Assert.NotNull(v);
        Assert.Equal(15566, v!.Id);
    }

    /// <summary>צלעות טלה: weighted no-variation product — parent row shows per-unit weight from order lines.</summary>
    [Fact]
    public void ResolveNoVariationParentUnitWeightKg_UsesLineWeightWhenConsistent()
    {
        var p = new Product { Id = 1, Name = "צלעות טלה", IsWeighted = true };
        var w = QuantityConcentrationReportService.ResolveNoVariationParentUnitWeightKg(
            p, new decimal?[] { 0.4m, 0.4m, null });
        Assert.Equal(0.4m, w);
    }

    [Fact]
    public void ResolveNoVariationParentUnitWeightKg_MixedLineWeights_ReturnsNull()
    {
        var p = new Product { Id = 1, Name = "צלעות טלה", IsWeighted = true };
        var w = QuantityConcentrationReportService.ResolveNoVariationParentUnitWeightKg(
            p, new decimal?[] { 0.4m, 0.6m });
        Assert.Null(w);
    }

    [Fact]
    public void ResolveNoVariationParentUnitWeightKg_NoLineWeights_FallsBackToCatalogConfig()
    {
        var p = new Product
        {
            Id = 1,
            Name = "צלעות טלה",
            IsWeighted = true,
            SetupType = new SetupType { Name = "by_unit" },
            WeightConfig = new WeightConfig
            {
                UnitWeight = "400",
                Unit = new Unit { Name = "g" },
            },
        };
        var w = QuantityConcentrationReportService.ResolveNoVariationParentUnitWeightKg(
            p, new decimal?[] { null });
        Assert.Equal(0.4m, w);
    }

    [Fact]
    public void ResolveNoVariationParentUnitWeightKg_ProductWithVariants_ReturnsNull()
    {
        var p = new Product
        {
            Id = 1,
            Name = "אנטריקוט על עצם",
            IsWeighted = true,
            ProductVariant = new List<ProductVariant> { new() { Id = 5, IsDeleted = false } },
        };
        var w = QuantityConcentrationReportService.ResolveNoVariationParentUnitWeightKg(
            p, new decimal?[] { 0.75m });
        Assert.Null(w);
    }

    [Fact]
    public void EnrichDetailLinesWithVariationStock_WeightedProduct_UsesStockKg()
    {
        var p = new Product
        {
            Id = 1,
            Name = "סטייק סינטה",
            IsWeighted = true,
            VariationStockByQuantity = true,
            StockManagementType = new StockManagementType { Name = "variation" },
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 1,
                    IsDeleted = false,
                    StockQuantity = 99.16m,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionValue = "עובי אצבע" },
                    },
                },
            },
        };
        var lines = new List<QuantityConcentrationLineDto>
        {
            new()
            {
                LineLabel = "עובי אצבע",
                VariantId = 1,
                QuantityKg = 0.8m,
                QuantityUnits = 2m,
                WeightPerUnitKg = 0.4m,
            },
        };
        var result = QuantityConcentrationReportService.EnrichDetailLinesWithVariationStock(lines, p);
        Assert.Single(result);
        Assert.Equal(99.16m, result[0].StockKg);
        Assert.Null(result[0].StockUnits);
        Assert.Equal("ק״ג", result[0].StockUnitLabel);
    }

    [Fact]
    public void EnrichDetailLinesWithVariationStock_ByUnitSetupWithoutIsWeightedFlag_UsesStockKg()
    {
        var p = new Product
        {
            Id = 2,
            Name = "סטייק סינטה",
            IsWeighted = false,
            SetupType = new SetupType { Name = "by_unit" },
            VariationStockByQuantity = true,
            StockManagementType = new StockManagementType { Name = "variation" },
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 2,
                    IsDeleted = false,
                    StockQuantity = 12.5m,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionValue = "נתח שלם" },
                    },
                },
            },
        };
        var lines = new List<QuantityConcentrationLineDto>
        {
            new() { LineLabel = "נתח שלם", VariantId = 2 },
        };
        var result = QuantityConcentrationReportService.EnrichDetailLinesWithVariationStock(lines, p);
        Assert.Single(result);
        Assert.Equal(12.5m, result[0].StockKg);
        Assert.Null(result[0].StockUnits);
        Assert.Equal("ק״ג", result[0].StockUnitLabel);
    }
}
