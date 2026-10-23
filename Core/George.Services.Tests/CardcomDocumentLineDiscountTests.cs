using George.DB;
using George.Services.Payments.Cardcom;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Site.InvoicePromotionDisplayMode = "line_discount" (הקצב בעיר, Cardcom support 2026-10-14): every discounted
/// product keeps its catalog price and gets its own negative "הנחה - {מבצע} ({מוצר})" line right under it, so the
/// invoice shows which product got which deal. Σ lines still equals the charged total; no ADJUSTMENT for promotions.
/// </summary>
public class CardcomDocumentLineDiscountTests
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
    public void Line_discount_puts_a_named_negative_line_under_each_discounted_product_and_needs_no_adjustment()
    {
        var o = Order3386();
        var doc = CardcomDocumentBuilder.Build(o, o.OrderItem, promotionDisplayMode: CardcomDocumentBuilder.PromotionDisplayLineDiscount);

        var lines = doc.Products.ToList();
        var beefIdx = lines.FindIndex(l => l.Description.StartsWith("בשר טחון"));
        Assert.Equal(79m, lines[beefIdx].UnitCost);
        Assert.Equal(171.43m, lines[beefIdx].TotalLineCost);

        var beefDiscount = lines[beefIdx + 1];
        Assert.Equal(CardcomDocumentBuilder.DiscountLineProductId, beefDiscount.ProductId);
        Assert.Equal("הנחה - 2 ק\"ג בשר טחון פרימיום ב-120 (בשר טחון טרי פרימיום)", beefDiscount.Description);
        Assert.Equal(1m, beefDiscount.Quantity);
        Assert.Equal(-41.23m, beefDiscount.UnitCost);

        Assert.Equal(3, lines.Count(l => l.ProductId == CardcomDocumentBuilder.DiscountLineProductId));
        Assert.DoesNotContain(lines, l => l.ProductId == "ADJUSTMENT");
        Assert.Equal(549.25m, DocumentTotal(doc));
    }

    [Fact]
    public void Resolve_prefers_the_mode_column_and_falls_back_to_the_legacy_bool()
    {
        Assert.Equal("line_discount", CardcomDocumentBuilder.ResolvePromotionDisplayMode("line_discount", false));
        Assert.Equal("net_price", CardcomDocumentBuilder.ResolvePromotionDisplayMode(null, true));
        Assert.Equal("discount_total", CardcomDocumentBuilder.ResolvePromotionDisplayMode("bogus", null));
    }
}
