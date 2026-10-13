using George.DB;
using George.Services;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// הקצב בעיר Woo #3386 / George 9013 (2026-09-07): three buy_x_pay_y promotions, plugin sends lines[] empty
/// for each, so the order-level fallback spread every promotion over ALL lines and grossed them all up by
/// ×1.1486. At picking the evaluator re-derived discounts per product, leaving the two untouched products
/// (85 ₪/kg) at 97.64 ₪/kg - ₪23 overcharged. Each promotion's baked discount must land on its own lines only.
/// </summary>
public class PromotionPayloadTargetingTests
{
    private static List<OrderItem> Order3386Lines() => new()
    {
        new OrderItem { Id = 1, ProductId = 3065, Quantity = 2m, PricePerUnit = 60m, TotalPrice = 120m },  // בשר טחון - promo 31 (38₪)
        new OrderItem { Id = 2, ProductId = 3258, Quantity = 2m, PricePerUnit = 45m, TotalPrice = 90m },   // עוף טחון - promo 32 (20₪)
        new OrderItem { Id = 3, ProductId = 3272, Quantity = 0.5m, PricePerUnit = 85m, TotalPrice = 42.5m }, // no promo
        new OrderItem { Id = 4, ProductId = 3242, Quantity = 2m, PricePerUnit = 60m, TotalPrice = 120m },  // פילה עוף - promo 26 (10₪)
        new OrderItem { Id = 5, ProductId = 3257, Quantity = 1m, PricePerUnit = 85m, TotalPrice = 85m },   // no promo
    };

    [Fact]
    public void Baked_discount_targeted_to_its_lines_restores_the_catalog_price_and_leaves_other_lines_alone()
    {
        var items = Order3386Lines();

        OrderService.DistributeOrderLevelPromotionDiscount(
            OrderService.SelectPromotionTargetLines(items, new HashSet<int> { 3242 }), 26, 10m, grossUpLineTotals: true);
        OrderService.DistributeOrderLevelPromotionDiscount(
            OrderService.SelectPromotionTargetLines(items, new HashSet<int> { 3065 }), 31, 38m, grossUpLineTotals: true);
        OrderService.DistributeOrderLevelPromotionDiscount(
            OrderService.SelectPromotionTargetLines(items, new HashSet<int> { 3258 }), 32, 20m, grossUpLineTotals: true);

        Assert.Equal(79m, items[0].PricePerUnit);
        Assert.Equal(158m, items[0].TotalPrice);
        Assert.Equal(38m, items[0].DiscountAmount);
        Assert.Equal(31, items[0].PromotionId);

        Assert.Equal(55m, items[1].PricePerUnit);
        Assert.Equal(20m, items[1].DiscountAmount);

        Assert.Equal(65m, items[3].PricePerUnit);
        Assert.Equal(130m, items[3].TotalPrice);
        Assert.Equal(10m, items[3].DiscountAmount);

        // Lines no promotion touched keep their price - this is what picking later multiplies by the weight.
        foreach (var untouched in new[] { items[2], items[4] })
        {
            Assert.Equal(85m, untouched.PricePerUnit);
            Assert.Null(untouched.DiscountAmount);
            Assert.Null(untouched.PromotionId);
        }

        // Net of every line still equals what Woo charged.
        Assert.Equal(457.5m, items.Sum(i => (i.TotalPrice ?? 0m) - (i.DiscountAmount ?? 0m)));
    }

    [Fact]
    public void Unknown_targets_fall_back_to_all_lines()
    {
        var items = Order3386Lines();
        Assert.Same(items, OrderService.SelectPromotionTargetLines(items, new HashSet<int>()));
        Assert.Same(items, OrderService.SelectPromotionTargetLines(items, new HashSet<int> { 999999 }));
    }
}
