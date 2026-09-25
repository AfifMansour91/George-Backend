using George.Services.Payments;
using George.Services.Payments.PayPlus;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// PEPE #20 (23/9): four weighed lines (439.82) with a 10% manual discount, charged 384.86 - the Invoice+ document
/// carried the lines and the payment with nothing in between, and PayPlus refused it
/// ("items-total-not-equal-to-calculated-total"). Discounts now ride on the lines as discount_value.
/// </summary>
public class PayPlusDocumentDiscountTests
{
    private static PayPlusDocumentProductLine Line(string name, decimal qty, decimal unit) =>
        new() { Description = name, Quantity = qty, UnitCost = unit };

    private static decimal Net(IEnumerable<PayPlusDocumentProductLine> lines) => Math.Round(lines.Sum(l => l.NetTotal), 2);

    [Fact]
    public void ManualDiscount_IsSpreadOverLines_AndNetsToThePayment()
    {
        var items = new List<PayPlusDocumentProductLine>
        {
            Line("סלמון 500 גר'", 0.534m, 139.9m),   // 74.71
            Line("טונה 1 ק\"ג", 1.018m, 199.9m),      // 203.50
            Line("דניס 500 גר'", 0.548m, 99.9m),     // 54.75
            Line("לברק 1 ק\"ג", 0.594m, 179.9m),     // 106.86
        };

        var result = PaymentService.ReconcilePayPlusDocumentLines(items, 384.86m);

        Assert.Equal(4, result.Count);
        Assert.Equal(54.96m, result.Sum(l => l.DiscountAmount));
        Assert.Equal(384.86m, Net(result));
        // Proportional: the biggest line carries the biggest share (and the rounding remainder).
        Assert.True(result[1].DiscountAmount > result[0].DiscountAmount);
        Assert.All(result, l => Assert.True(l.DiscountAmount >= 0m));
    }

    [Fact]
    public void NoDiscount_LeavesLinesUntouched()
    {
        var items = new List<PayPlusDocumentProductLine> { Line("a", 2, 10m), Line("b", 1, 5.5m) };
        var result = PaymentService.ReconcilePayPlusDocumentLines(items, 25.5m);
        Assert.Same(items, result);
        Assert.All(result, l => Assert.Equal(0m, l.DiscountAmount));
    }

    [Fact]
    public void LinesBelowPayment_GetOneAdjustmentLine()
    {
        var items = new List<PayPlusDocumentProductLine> { Line("a", 1, 100m) };
        var result = PaymentService.ReconcilePayPlusDocumentLines(items, 100.01m);
        Assert.Equal(2, result.Count);
        Assert.Equal("התאמת סכום", result[1].Description);
        Assert.Equal(0.01m, result[1].UnitCost);
        Assert.Equal(100.01m, Net(result));
    }

    [Fact]
    public void OneAgoraDrift_IsAbsorbedByALine()
    {
        // The refund-side twin of the same order: header 384.86 vs charge 384.85.
        var items = new List<PayPlusDocumentProductLine> { Line("a", 1, 200m), Line("b", 1, 184.86m) };
        var result = PaymentService.ReconcilePayPlusDocumentLines(items, 384.85m);
        Assert.Equal(384.85m, Net(result));
        Assert.Equal(0.01m, result.Sum(l => l.DiscountAmount));
    }

    // PEPE 11717 (24/9): six weighed/unit lines (925.21) with a 5% manual discount, charged 883.01.
    private static List<PayPlusDocumentProductLine> Pepe11717() => new()
    {
        Line("אנטריקוט", 2.056m, 119.9m),   // 246.51
        Line("פילה", 1.024m, 129.9m),       // 133.02
        Line("שניצל", 2.792m, 64.9m),       // 181.20
        Line("פרגית", 2.108m, 79.9m),       // 168.43
        Line("שוקיים עוף", 3.262m, 47.9m),  // 156.25
        Line("מוצר כללי", 1m, 39.8m),       // 39.80
    };

    [Fact]
    public void DiscountLineShape_AddsOneNegativeLine_AndKeepsTheOriginalPrices()
    {
        var items = Pepe11717();
        var result = PaymentService.ReconcilePayPlusDocumentLines(items, 883.01m, PayPlusDiscountShape.DiscountLine);

        Assert.Equal(7, result.Count);
        var discount = result[^1];
        Assert.Equal("הנחה", discount.Description);
        Assert.Equal(1m, discount.Quantity);
        Assert.Equal(-42.20m, discount.UnitCost);
        Assert.Equal(883.01m, Net(result));
        Assert.All(result.Take(6), l => Assert.Equal(0m, l.DiscountAmount));
        Assert.Equal(119.9m, result[0].UnitCost);
    }

    [Fact]
    public void NetUnitPricesShape_HasOnlyPositiveLines_NoDiscountFields_AndNetsToThePayment()
    {
        var items = Pepe11717();
        var result = PaymentService.ReconcilePayPlusDocumentLines(items, 883.01m, PayPlusDiscountShape.NetUnitPrices);

        Assert.Equal(883.01m, Net(result));
        Assert.All(result, l =>
        {
            Assert.True(l.UnitCost > 0m);
            Assert.Equal(0m, l.DiscountAmount);
        });
        // Unit prices are lowered (rounded down), the agorot land in one positive remainder line.
        Assert.True(result[0].UnitCost < 119.9m);
        Assert.Equal("התאמת סכום", result[^1].Description);
        Assert.True(result[^1].UnitCost > 0m && result[^1].UnitCost <= 0.15m); // at most 0.01 × Σ quantity (12.24 kg/units here)
        Assert.Equal(6, result.Count(l => l.Description != "התאמת סכום"));
    }

    [Fact]
    public void AllShapes_AgreeOnTheDefaultAndOnLinesBelowThePayment()
    {
        foreach (var shape in new[] { PayPlusDiscountShape.PerLineDiscount, PayPlusDiscountShape.DiscountLine, PayPlusDiscountShape.NetUnitPrices })
        {
            var items = Pepe11717();
            Assert.Equal(883.01m, Net(PaymentService.ReconcilePayPlusDocumentLines(items, 883.01m, shape)));

            var below = new List<PayPlusDocumentProductLine> { Line("a", 2.294m, 66.9m), Line("b", 1.662m, 99.9m), Line("משלוח", 1, 15m) };
            var adjusted = PaymentService.ReconcilePayPlusDocumentLines(below, 334.51m, shape);
            Assert.Equal("התאמת סכום", adjusted[^1].Description);
            Assert.Equal(0.01m, adjusted[^1].UnitCost);

            var exact = new List<PayPlusDocumentProductLine> { Line("a", 2, 10m) };
            Assert.Same(exact, PaymentService.ReconcilePayPlusDocumentLines(exact, 20m, shape));
        }
    }

    [Fact]
    public void DocumentFailure_IsDescribedInHebrew_KeepingTheSlug()
    {
        var text = PaymentService.DescribePayPlusDocumentFailure("items-total-not-equal-to-calculated-total");
        Assert.Contains("סכום הפריטים", text);
        Assert.Contains("(items-total-not-equal-to-calculated-total)", text);
        Assert.Contains("unique-identifier-exists", PaymentService.DescribePayPlusDocumentFailure("unique-identifier-exists"));
        Assert.Contains("some-new-error", PaymentService.DescribePayPlusDocumentFailure("some-new-error"));
        Assert.Contains("PayPlus", PaymentService.DescribePayPlusDocumentFailure(null));
    }

    [Fact]
    public void FullDiscountOrEmptyPayment_IsLeftAlone()
    {
        var items = new List<PayPlusDocumentProductLine> { Line("a", 1, 10m) };
        Assert.Same(items, PaymentService.ReconcilePayPlusDocumentLines(items, null));
        Assert.Same(items, PaymentService.ReconcilePayPlusDocumentLines(items, 0m));
        Assert.Empty(PaymentService.ReconcilePayPlusDocumentLines(new List<PayPlusDocumentProductLine>(), 10m));
    }
}
