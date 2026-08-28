using George.DB;
using George.Services;
using George.Services.Request;

namespace George.Services.Tests;

public class WooPercentEncodedTextTests
{
    [Theory]
    [InlineData("%d7%9c%d7%9c%d7%90-%d7%a2%d7%95%d7%a8", "ללא-עור")]
    [InlineData("%d7%98%d7%97%d7%99%d7%a0%d7%94-%d7%9b%d7%a4%d7%95%d7%9c%d7%94", "טחינה-כפולה")]
    [InlineData("ללא-עור", "ללא-עור")]
    [InlineData("פרוס", "פרוס")]
    [InlineData("50% off", "50% off")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void Decode_HandlesEncodedAndPlainValues(string? input, string? expected)
    {
        Assert.Equal(expected, WooPercentEncodedText.Decode(input));
    }

    // Woo order for a variant whose catalog option value is still the percent-encoded slug
    // (catalogs imported before the 2026-08-20 decode fix) - the persisted cutting label and
    // LineDisplayJson must render readable Hebrew, not the slug (דגי גת order 5832 item 12133).
    [Fact]
    public void MergeComputedDisplayFields_DecodesEncodedCatalogCuttingValue()
    {
        var product = new Product
        {
            Id = 13655,
            Name = "פילה אינטיאס",
            IsWeighted = true,
            SetupType = new SetupType { Name = "by_unit" },
            WeightConfig = new WeightConfig
            {
                UnitWeightMode = new UnitWeightMode { Name = "variable" },
            },
            ProductVariant = new List<ProductVariant>
            {
                new()
                {
                    Id = 24987,
                    IsDeleted = false,
                    ProductVariantOptionValue = new List<ProductVariantOptionValue>
                    {
                        new() { OptionName = "צורת חיתוך", OptionValue = "%d7%9c%d7%9c%d7%90-%d7%a2%d7%95%d7%a8" },
                    },
                },
            },
        };
        var req = new CreateOrderItemReq
        {
            ProductId = 13655,
            ProductVariantId = 24987,
            Quantity = 0.4m,
            SaleTotalWeight = "400 גר'",
        };
        var item = new OrderItem { Quantity = 0.4m };

        OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(item, req, product);

        Assert.Equal("ללא-עור", item.OrderLineCuttingLabel);
        Assert.DoesNotContain("%d7", item.LineDisplayJson ?? "");
    }
}
