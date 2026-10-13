using George.DB;
using George.Services.Payments.Cardcom;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Site.InvoicePromotionAsLinePrice (הקצב בעיר, 2026-10-13): promotion lines printed at the deal's net ₪/kg with
/// the promotion name, no "הנחה" adjustment line. Off = the document stays exactly as before (catalog ₪/kg per
/// line + ADJUSTMENT). Numbers are order 9013 / Woo #3386 after the multi-promotion ingest fix.
/// </summary>
public class CardcomDocumentPromotionLineTests
{
    private static Order Order3386()
    {
        var o = new Order { Id = 9013, OrderNumber = "7", Total = 549.25m, ShippingCost = 30m, CustomerName = "מיכל בלמס", DeliveryType = "Pickup" };
        o.OrderItem = new List<OrderItem>
        {
            new() { Id = 1, OrderId = 9013, ProductId = 3065, Title = "בשר טחון טרי פרימיום", Quantity = 2m, PickedQuantity = 2.17m, PricePerUnit = 79m, TotalPrice = 171.43m, DiscountAmount = 41.23m, PromotionId = 31, Promotion = new Promotion { Id = 31, Name = "2 ק\"ג בשר טחון פרימיום ב-120" }, SortOrder = 1 },
            new() { Id = 2, OrderId = 9013, ProductId = 3258, Title = "עוף טחון", Quantity = 2m, PickedQuantity = 2.34m, PricePerUnit = 55m, TotalPrice = 128.70m, DiscountAmount = 20m, PromotionId = 32, Promotion = new Promotion { Id = 32, Name = "2 ק\"ג טחון עוף ב-90" }, SortOrder = 2 },
            new() { Id = 3, OrderId = 9013, ProductId = 3272, Title = "טחון פרגית עוף טרי", Quantity = 0.5m, PickedQuantity = 0.73m, PricePerUnit = 85m, TotalPrice = 62.05m, SortOrder = 3 },
            new() { Id = 4, OrderId = 9013, ProductId = 3242, Title = "פילה עוף", Quantity = 2m, PickedQuantity = 2.08m, PricePerUnit = 65m, TotalPrice = 135.20m, DiscountAmount = 10.40m, PromotionId = 26, Promotion = new Promotion { Id = 26, Name = "2 ק\"ג פילה עוף פרימיום ב-120" }, SortOrder = 4 },
            new() { Id = 5, OrderId = 9013, ProductId = 3257, Title = "סטייק פרגית", Quantity = 1m, PickedQuantity = 1.10m, PricePerUnit = 85m, TotalPrice = 93.50m, SortOrder = 5 },
        };
        return o;
    }

    private static decimal DocumentTotal(CardcomTransactionDocument doc) =>
        doc.Products.Sum(l => l.TotalLineCost is > 0 ? l.TotalLineCost.Value : Math.Round(l.Quantity * l.UnitCost, 2, MidpointRounding.AwayFromZero));

    [Fact]
    public void Off_keeps_catalog_prices_and_reconciles_with_an_adjustment_line()
    {
        var o = Order3386();
        var doc = CardcomDocumentBuilder.Build(o, o.OrderItem);

        var beef = doc.Products.First(l => l.Description.StartsWith("בשר טחון"));
        Assert.Equal(79m, beef.UnitCost);
        Assert.Equal(171.43m, beef.TotalLineCost);
        Assert.DoesNotContain("ב-120", beef.Description);

        var adjustment = doc.Products.Single(l => l.ProductId == "ADJUSTMENT");
        Assert.Equal("הנחה", adjustment.Description);
        Assert.Equal(-71.63m, adjustment.UnitCost);
        Assert.Equal(549.25m, DocumentTotal(doc));
    }

    [Fact]
    public void On_prints_promotion_lines_at_net_price_per_kg_with_the_promotion_name_and_no_discount_line()
    {
        var o = Order3386();
        var doc = CardcomDocumentBuilder.Build(o, o.OrderItem, promotionAsLinePrice: true);

        var beef = doc.Products.First(l => l.Description.StartsWith("בשר טחון"));
        Assert.Equal(2.17m, beef.Quantity);
        Assert.Equal(60.00m, beef.UnitCost);
        Assert.Equal(130.20m, beef.TotalLineCost);
        Assert.EndsWith(" - 2 ק\"ג בשר טחון פרימיום ב-120", beef.Description);

        var chicken = doc.Products.First(l => l.Description.StartsWith("עוף טחון"));
        Assert.Equal(46.45m, chicken.UnitCost); // 108.70 ÷ 2.34 - full-price overage blended into one line
        Assert.Equal(108.70m, chicken.TotalLineCost);

        // Lines without a promotion are untouched.
        var steak = doc.Products.First(l => l.Description.StartsWith("סטייק פרגית"));
        Assert.Equal(85m, steak.UnitCost);
        Assert.DoesNotContain(" - ", steak.Description);

        Assert.DoesNotContain(doc.Products, l => l.ProductId == "ADJUSTMENT");
        Assert.Equal(549.25m, DocumentTotal(doc));
    }

    [Fact]
    public void On_without_a_loaded_promotion_name_falls_back_to_a_generic_label()
    {
        var o = Order3386();
        foreach (var it in o.OrderItem) it.Promotion = null;
        var doc = CardcomDocumentBuilder.Build(o, o.OrderItem, promotionAsLinePrice: true);
        var beef = doc.Products.First(l => l.Description.StartsWith("בשר טחון"));
        Assert.EndsWith(" - מבצע", beef.Description);
    }
}
