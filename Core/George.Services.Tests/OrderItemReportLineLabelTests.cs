using George.DB;
using George.Services;

namespace George.Services.Tests;

public class OrderItemReportLineLabelTests
{
    [Fact]
    public void ResolveOptionDisplayLabel_PrefersVariantTitle_OverComputedFields()
    {
        var line = new OrderItem
        {
            VariantTitle = "עובי אצבע (כ 200 גר')",
            OrderLineCuttingLabel = "עובי אצבע",
            OrderLineSizeLabel = "(כ 200 גרם)",
        };
        var label = OrderItemReportLineLabel.ResolveOptionDisplayLabel(line, "סטייק סינטה");
        Assert.Equal("עובי אצבע (כ 200 גר')", label);
    }

    [Fact]
    public void IsNonOptionDisplayLabel_TreatsUnitsAndPerUnitWeightAsNonOption()
    {
        Assert.True(OrderItemReportLineLabel.IsNonOptionDisplayLabel("500 גרם ליח'"));
        Assert.True(OrderItemReportLineLabel.IsNonOptionDisplayLabel("3 יח'"));
        Assert.False(OrderItemReportLineLabel.IsNonOptionDisplayLabel("עובי אצבע (כ 200 גר')"));
    }

    [Fact]
    public void BuildLineLabel_DoesNotConcatenateCutAndSize()
    {
        var line = new OrderItem
        {
            OrderLineCuttingLabel = "עובי אצבע (כ 200 גר')",
            OrderLineSizeLabel = "(כ 200 גרם)",
        };
        var label = OrderItemReportLineLabel.BuildLineLabel(line);
        Assert.Equal("עובי אצבע (כ 200 גר')", label);
    }
}
