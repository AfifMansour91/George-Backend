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

    [Fact]
    public void FullDiscountOrEmptyPayment_IsLeftAlone()
    {
        var items = new List<PayPlusDocumentProductLine> { Line("a", 1, 10m) };
        Assert.Same(items, PaymentService.ReconcilePayPlusDocumentLines(items, null));
        Assert.Same(items, PaymentService.ReconcilePayPlusDocumentLines(items, 0m));
        Assert.Empty(PaymentService.ReconcilePayPlusDocumentLines(new List<PayPlusDocumentProductLine>(), 10m));
    }
}
