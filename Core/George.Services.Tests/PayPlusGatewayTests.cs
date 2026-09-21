using George.Services;
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

    // PEPE 14/9 (order 10279): Invoice+ answered {"status":"success","details":{...}} for invoices 4003/4004 and
    // credit note 5003, and the gateway logged all three as "document creation failed" (it looked for docUID
    // at the root). The order kept no invoice, no SMS went out, and the manual retry created a duplicate.
    [Fact]
    public void ParseDocumentResult_RootStatusSuccessWithDetails_IsSuccessAndReadsNumberAndUrl()
    {
        const string json = "{\"status\":\"success\",\"details\":{\"doc_type\":\"inv_tax_receipt\",\"docUID\":\"c6d98ea5-617d-4afe-93aa-f002fdd38d37\",\"ita_confirmation_number\":null,\"requires_ita_confirmation\":false,\"number\":\"4003\",\"originalDocAddress\":\"https://restapi.payplus.co.il/getdoc/s/o/c6d98ea5.pdf\"}}";

        var result = PayPlusGateway.ParseDocumentResult(json);

        Assert.True(result.Success);
        Assert.Equal("4003", result.DocumentNumber);
        Assert.Equal("https://restapi.payplus.co.il/getdoc/s/o/c6d98ea5.pdf", result.DocumentUrl);
    }

    [Fact]
    public void ParseDocumentResult_RootFailure_SurfacesErrorCode()
    {
        const string json = "{\"status\":\"failure\",\"operation_id\":\"113a5262\",\"error\":\"unique-identifier-exists\",\"error_code\":215}";

        var result = PayPlusGateway.ParseDocumentResult(json);

        Assert.False(result.Success);
        Assert.Equal("unique-identifier-exists", result.Description);
    }

    [Fact]
    public void ParseDocumentResult_FlatRootFields_StillSupported()
    {
        const string json = "{\"docUID\":\"abc\",\"number\":\"12\",\"originalDocAddress\":\"https://x/12.pdf\"}";

        var result = PayPlusGateway.ParseDocumentResult(json);

        Assert.True(result.Success);
        Assert.Equal("12", result.DocumentNumber);
        Assert.Equal("https://x/12.pdf", result.DocumentUrl);
    }

    [Fact]
    public void BuildHostedPageItems_BufferLine_DoesNotSayNotCharged()
    {
        var items = PayPlusGateway.BuildHostedPageItems(new[] { Line("טסט - 500 גר'", 1, 2.50m) }, 3.00m);

        Assert.Equal(2, items.Count);
        Assert.Equal(0.50m, items[1]["price"]);
        var name = (string)items[1]["name"]!;
        Assert.DoesNotContain("לא נגבה", name);
        Assert.Contains("זמנית", name);
    }

    // PEPE 14/9 order 10280: line "טסט", VariantTitle ק"ג, 0.5 kg at 5.00/kg - the page showed "טסט - ק״ג", quantity 1.
    [Fact]
    public void BuildPayPlusHostedLineName_WeighedLine_ShowsOrderedWeight_NotUnitOfSaleTitle()
    {
        var line = new George.DB.OrderItem
        {
            Title = "טסט",
            VariantTitle = "ק\"ג",
            Quantity = 1m,
            UnitWeightGrams = 500m,
            SaleTotalWeight = "500 גר'",
            SaleUnits = "1 יח'",
            OrderLineQuantityMode = "weight",
            PricePerUnit = 5.00m,
            TotalPrice = 2.50m,
        };

        // The ordered weight leads the name (PEPE 21/9) - see the trays test below.
        Assert.Equal("500 גר' - טסט", PaymentService.BuildPayPlusHostedLineName(line, isWholeUnits: false));
    }

    // PEPE 21/9: "בקר טחון טרי - 2 - 500 גר'" read as two packs of 500 g; the "2" is the trays split.
    [Fact]
    public void BuildPayPlusHostedLineName_TraysVariation_WeightFirst_AndNumericOptionNamed()
    {
        var line = new George.DB.OrderItem
        {
            Title = "בקר טחון טרי",
            VariantTitle = "2",
            Quantity = 1m,
            UnitWeightGrams = 500m,
            SaleTotalWeight = "500 גר'",
            OrderLineQuantityMode = "weight",
            PricePerUnit = 79.90m,
            TotalPrice = 39.95m,
            LineDisplayJson = "{\"v\":1,\"kind\":\"by_weight\",\"sizeName\":\"2\",\"sizeOptionName\":\"חלוקה למגשים\",\"totalWeightGrams\":500}",
        };

        Assert.Equal("500 גר' - בקר טחון טרי - חלוקה למגשים: 2", PaymentService.BuildPayPlusHostedLineName(line, isWholeUnits: false));
    }

    [Fact]
    public void BuildPayPlusHostedLineName_WholeUnitsWithRealOption_KeepsOption_NoQuantitySuffix()
    {
        var line = new George.DB.OrderItem { Title = "סטייק אנטריקוט", VariantTitle = "עובי אצבע", Quantity = 2m, PricePerUnit = 60m, TotalPrice = 120m };

        Assert.Equal("סטייק אנטריקוט - עובי אצבע", PaymentService.BuildPayPlusHostedLineName(line, isWholeUnits: true));
    }

    [Fact]
    public void BuildPayPlusHostedLineName_GenericLine_UsesItsTitle()
    {
        var line = new George.DB.OrderItem { Title = "מוצר כללי", Quantity = 1m, PricePerUnit = 1.50m, TotalPrice = 1.50m };

        Assert.Equal("מוצר כללי", PaymentService.BuildPayPlusHostedLineName(line, isWholeUnits: true));
    }

    // PEPE invoice 4005 (9/16): "טסט - ק״ג" - the unit-of-sale variant title leaked into the document line.
    [Fact]
    public void FormatDocumentLineDescription_DropsUnitOfSaleVariantTitle_KeepsRealOption()
    {
        Assert.Equal("טסט", OrderItemLineDisplay.FormatDocumentLineDescription(new George.DB.OrderItem { Title = "טסט", VariantTitle = "ק\"ג" }));
        Assert.Equal("טסט", OrderItemLineDisplay.FormatDocumentLineDescription(new George.DB.OrderItem { Title = "טסט", VariantTitle = "יחידה" }));
        Assert.Equal("סטייק אנטריקוט - עובי אצבע",
            OrderItemLineDescriptionOf("סטייק אנטריקוט", "עובי אצבע"));
        Assert.Equal("פריט", OrderItemLineDisplay.FormatDocumentLineDescription(new George.DB.OrderItem { Title = "  ", VariantTitle = null }));
    }

    private static string OrderItemLineDescriptionOf(string title, string variant)
        => OrderItemLineDisplay.FormatDocumentLineDescription(new George.DB.OrderItem { Title = title, VariantTitle = variant });
}
