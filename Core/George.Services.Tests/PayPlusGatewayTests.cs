using George.Services.Payments;
using George.Services.Payments.PayPlus;
using Xunit;

namespace George.Services.Tests;

public class PayPlusGatewayTests
{
    private static HostedSessionLineItem Line(string name, decimal qty, decimal price, bool shipping = false)
        => new() { Name = name, Quantity = qty, Price = price, IsShipping = shipping };

    [Fact]
    public void BuildHostedPageItems_LinesMatchAmount_NoAdjustmentLine()
    {
        var items = PayPlusGateway.BuildHostedPageItems(
            new[] { Line("מוצר כללי", 1, 1.50m), Line("משלוח", 1, 10m, shipping: true) }, 11.50m);

        Assert.Equal(2, items.Count);
        Assert.Equal("מוצר כללי", items[0]["name"]);
        Assert.Equal(true, items[1]["shipping"]);
    }

    [Fact]
    public void BuildHostedPageItems_HoldBufferAboveLines_AddsVisibleBufferLine()
    {
        // Order total 1.50, authorization amount 3.13 (weighed-order buffer): PayPlus wants the lines to add
        // up to `amount`, so the buffer is shown as its own line instead of the list being rejected.
        var items = PayPlusGateway.BuildHostedPageItems(new[] { Line("מוצר כללי", 1, 1.50m) }, 3.13m);

        Assert.Equal(2, items.Count);
        Assert.Equal(1.63m, items[1]["price"]);
        Assert.Contains("מסגרת", (string)items[1]["name"]!);
    }

    [Fact]
    public void BuildHostedPageItems_LinesAboveAmount_DropsItems()
    {
        var items = PayPlusGateway.BuildHostedPageItems(new[] { Line("א", 2, 10m) }, 15m);

        Assert.Empty(items);
    }

    [Fact]
    public void BuildHostedPageItems_NoLines_Empty()
    {
        Assert.Empty(PayPlusGateway.BuildHostedPageItems(null, 5m));
        Assert.Empty(PayPlusGateway.BuildHostedPageItems(Array.Empty<HostedSessionLineItem>(), 5m));
    }
}
