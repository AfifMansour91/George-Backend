using George.DB;
using George.Services;
using George.Services.Request;

namespace George.Services.Tests;

public class PromotionEvaluatorLimitsTests
{
    private static readonly PromotionEvaluator.SiteEvaluationDefaults Defaults = new();

    private static Promotion Promo(int id, string type, string payloadJson, string? discountKind = null, string? couponCode = null) => new()
    {
        Id = id,
        PromotionType = type,
        ListDiscountKind = discountKind,
        PayloadJson = payloadJson,
        CouponCode = couponCode,
        IsActive = true,
        IsDraft = false,
        IsDeleted = false,
        Name = $"Promo {id}",
        ChannelsJson = "[\"all\"]",
    };

    [Fact]
    public void BuyXPayY_perOrder_caps_bundle_applications()
    {
        var promo = Promo(1, "buy_x_pay_y", """
            {
              "condition": { "scope": "product", "productId": 10, "quantity": 3 },
              "pricing": { "fixedPrice": 25 },
              "limits": { "perOrder": 1 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "10", Quantity = 9, PricePerUnit = 10m },
            ],
            CartTotal = 90m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(5m, result.PromotionsApplied[0].DiscountAmount);
        Assert.Equal(5m, result.TotalDiscount);
    }

    [Fact]
    public void BuyXGetY_perOrder_caps_reward_discount()
    {
        var promo = Promo(2, "buy_x_get_y", """
            {
              "condition": { "productScope": "all", "minQuantity": 2 },
              "reward": { "productIds": [99], "discountType": "free", "maxQuantity": 1 },
              "limits": { "perOrder": 1 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 4, PricePerUnit = 20m },
                new EvaluateCartLine { ProductId = "99", Quantity = 2, PricePerUnit = 5m },
            ],
            CartTotal = 90m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(5m, result.PromotionsApplied[0].DiscountAmount);
    }

    [Fact]
    public void PerCustomer_limit_skips_promotion_when_prior_redemptions_reached()
    {
        var promo = Promo(3, "discount", """
            { "value": 10, "applyScope": "all", "limitPerCustomer": 1 }
            """, "percent");

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            CustomerId = "42",
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 100m },
            ],
            CartTotal = 100m,
        };

        var prior = new Dictionary<int, int> { [3] = 1 };
        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow, prior);

        Assert.Empty(result.PromotionsApplied);
        Assert.Equal(0m, result.TotalDiscount);
    }

    [Fact]
    public void BuyXPayY_perOrder_samePrice_overage_uses_bundle_unit_price()
    {
        var promo = Promo(5, "buy_x_pay_y", """
            {
              "condition": { "scope": "product", "productId": 10, "quantity": 3 },
              "pricing": { "fixedPrice": 30 },
              "overagePolicy": "same_price",
              "limits": { "perOrder": 1 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "10", Quantity = 5, PricePerUnit = 20m },
            ],
            CartTotal = 100m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        // 3 units @ bundle (30) + 2 overage @ 10 each = 50 vs list 100 → discount 50
        Assert.Equal(50m, result.PromotionsApplied[0].DiscountAmount);
    }

    [Fact]
    public void BuyXPayY_multiple_bundles_without_perOrderLimit()
    {
        var promo = Promo(6, "buy_x_pay_y", """
            {
              "condition": { "scope": "product", "productId": 10, "quantity": 3 },
              "pricing": { "fixedPrice": 25 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "10", Quantity = 6, PricePerUnit = 10m },
            ],
            CartTotal = 60m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(10m, result.PromotionsApplied[0].DiscountAmount);
        Assert.NotNull(result.PromotionsApplied[0].EligibleItems);
        Assert.Single(result.PromotionsApplied[0].EligibleItems!);
    }

    [Fact]
    public void BuyXPayY_multi_line_distributes_discount_by_consumed_value()
    {
        var promo = Promo(7, "buy_x_pay_y", """
            {
              "condition": { "scope": "category", "categoryId": 1, "quantity": 3 },
              "pricing": { "fixedPrice": 25 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 2, PricePerUnit = 20m, CategoryIds = ["1"] },
                new EvaluateCartLine { ProductId = "2", Quantity = 4, PricePerUnit = 10m, CategoryIds = ["1"] },
            ],
            CartTotal = 80m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(30m, result.PromotionsApplied[0].DiscountAmount);
        var items = result.PromotionsApplied[0].EligibleItems!;
        Assert.Equal(2, items.Count);
        Assert.Equal(15m, items.First(i => i.ProductId == "1").DiscountAmount);
        Assert.Equal(15m, items.First(i => i.ProductId == "2").DiscountAmount);
    }

    [Fact]
    public void BuyXPayY_gift_lines_excluded_from_buy_condition()
    {
        var promo = Promo(8, "buy_x_get_y", """
            {
              "condition": { "productScope": "all", "minQuantity": 2 },
              "reward": { "productIds": [99], "discountType": "free", "benefitQuantity": 1 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 20m },
                new EvaluateCartLine { ProductId = "99", Quantity = 1, PricePerUnit = 5m, Source = "promotion_gift" },
            ],
            CartTotal = 25m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Empty(result.PromotionsApplied);
        Assert.Single(result.PromotionsNearby);
    }

    [Fact]
    public void PerCustomer_limit_allows_when_under_cap()
    {
        var promo = Promo(4, "discount", """
            { "value": 10, "applyScope": "all", "limitPerCustomer": 2 }
            """, "percent");

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            CustomerId = "42",
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 100m },
            ],
            CartTotal = 100m,
        };

        var prior = new Dictionary<int, int> { [4] = 1 };
        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow, prior);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(10m, result.TotalDiscount);
    }

    [Fact]
    public void Discount_skips_catalog_discounted_lines_when_setting_disabled()
    {
        var promo = Promo(5, "discount", """
            { "value": 10, "applyScope": "products", "productIds": [1, 2] }
            """, "percent");

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 80m, IsCatalogDiscounted = true },
                new EvaluateCartLine { ProductId = "2", Quantity = 1, PricePerUnit = 100m },
            ],
            CartTotal = 180m,
        };

        var defaults = new PromotionEvaluator.SiteEvaluationDefaults { ApplyOnDiscountedProducts = false };
        var result = PromotionEvaluator.Evaluate([promo], req, defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(10m, result.TotalDiscount);
        Assert.Single(result.PromotionsApplied[0].EligibleItems!);
        Assert.Equal("2", result.PromotionsApplied[0].EligibleItems![0].ProductId);
    }

    [Fact]
    public void BuyXGetY_emits_auto_add_fields_when_reward_missing_from_cart()
    {
        var promo = Promo(6, "buy_x_get_y", """
            {
              "condition": { "productScope": "specific_products", "productIds": [1], "minQuantity": 4 },
              "reward": { "benefitAppliesTo": "products", "productIds": [99], "discountType": "free", "benefitQuantity": 2, "autoAdd": true }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 4, PricePerUnit = 10m },
            ],
            CartTotal = 40m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        var applied = result.PromotionsApplied[0];
        Assert.Equal(99, applied.RewardProductId);
        Assert.True(applied.AutoAddEligible);
        Assert.Equal(2, applied.AutoAddQuantity);
        Assert.Equal(0m, applied.DiscountAmount);
    }

    [Fact]
    public void BuyXGetY_auto_add_not_eligible_when_not_free()
    {
        var promo = Promo(7, "buy_x_get_y", """
            {
              "condition": { "productScope": "specific_products", "productIds": [1], "minQuantity": 2 },
              "reward": { "benefitAppliesTo": "products", "productIds": [99], "discountType": "percentage", "discountValue": 50, "autoAdd": true }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 2, PricePerUnit = 10m },
            ],
            CartTotal = 20m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.False(result.PromotionsApplied[0].AutoAddEligible);
    }

    [Fact]
    public void BuyXGetY_minAmount_uses_qualifying_buy_subtotal()
    {
        var promo = Promo(9, "buy_x_get_y", """
            {
              "condition": { "productScope": "specific_products", "productIds": [1], "minQuantity": 1, "minAmount": 100 },
              "reward": { "productIds": [99], "discountType": "free" }
            }
            """);

        var reqBelow = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 2, PricePerUnit = 20m },
                new EvaluateCartLine { ProductId = "99", Quantity = 1, PricePerUnit = 5m },
            ],
            CartTotal = 200m,
        };

        var below = PromotionEvaluator.Evaluate([promo], reqBelow, Defaults, DateTime.UtcNow);
        Assert.Empty(below.PromotionsApplied);
        Assert.Single(below.PromotionsNearby);
        Assert.Equal("amount", below.PromotionsNearby[0].Missing.Kind);

        var reqAbove = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 6, PricePerUnit = 20m },
                new EvaluateCartLine { ProductId = "99", Quantity = 1, PricePerUnit = 5m },
            ],
            CartTotal = 125m,
        };

        var above = PromotionEvaluator.Evaluate([promo], reqAbove, Defaults, DateTime.UtcNow);
        Assert.Single(above.PromotionsApplied);
        Assert.Equal(5m, above.PromotionsApplied[0].DiscountAmount);
    }

    [Fact]
    public void Discount_fixed_amount_is_single_basket_discount_not_per_line()
    {
        var promo = Promo(20, "discount", """
            { "value": 30, "applyScope": "all" }
            """, "amount");

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 2, PricePerUnit = 50m },
                new EvaluateCartLine { ProductId = "2", Quantity = 1, PricePerUnit = 40m },
            ],
            CartTotal = 140m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(30m, result.TotalDiscount);
        Assert.Equal(30m, result.PromotionsApplied[0].DiscountAmount);
    }

    [Fact]
    public void Discount_limitPerItems_applies_to_cheapest_units_first()
    {
        var promo = Promo(21, "discount", """
            { "value": 100, "applyScope": "products", "productIds": [1, 2], "limitPerItems": 2 }
            """, "percent");

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 100m },
                new EvaluateCartLine { ProductId = "2", Quantity = 1, PricePerUnit = 10m },
            ],
            CartTotal = 110m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        // 100% on cheapest (10) + most expensive (100) = 110
        Assert.Equal(110m, result.TotalDiscount);
    }

    [Fact]
    public void BuyXPayY_decimal_quantity_bundle()
    {
        var promo = Promo(22, "buy_x_pay_y", """
            {
              "condition": { "scope": "product", "productId": 10, "quantity": 1.5 },
              "pricing": { "fixedPrice": 45 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "10", Quantity = 1.5m, PricePerUnit = 40m },
            ],
            CartTotal = 60m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(15m, result.PromotionsApplied[0].DiscountAmount);
    }

    [Fact]
    public void Lower_priority_promotion_runs_first()
    {
        var low = Promo(30, "discount", """{ "value": 10, "applyScope": "products", "productIds": [1] }""", "percent");
        low.Priority = 5;
        var high = Promo(31, "discount", """{ "value": 10, "applyScope": "products", "productIds": [2] }""", "percent");
        high.Priority = 20;

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 100m },
                new EvaluateCartLine { ProductId = "2", Quantity = 1, PricePerUnit = 100m },
            ],
            CartTotal = 200m,
        };

        var result = PromotionEvaluator.Evaluate([high, low], req, Defaults, DateTime.UtcNow);

        Assert.Equal(2, result.PromotionsApplied.Count);
        Assert.Equal(30, result.PromotionsApplied[0].PromotionId);
        Assert.Equal(31, result.PromotionsApplied[1].PromotionId);
    }

    [Fact]
    public void Coupon_cap_keeps_highest_discount_among_matching_coupon_promotions()
    {
        var small = Promo(10, "discount", """{ "value": 10, "applyScope": "all" }""", "percent", "SAVE");
        var big = Promo(11, "discount", """{ "value": 20, "applyScope": "all" }""", "percent", "SAVE");

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            CouponCode = "SAVE",
            Cart =
            [
                new EvaluateCartLine { ProductId = "1", Quantity = 1, PricePerUnit = 100m },
            ],
            CartTotal = 100m,
        };

        var result = PromotionEvaluator.Evaluate([small, big], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        Assert.Equal(11, result.PromotionsApplied[0].PromotionId);
        Assert.Equal(20m, result.TotalDiscount);
    }

    [Fact]
    public void BuyXPayY_emits_eligible_items_for_order_stamping()
    {
        var promo = Promo(12, "buy_x_pay_y", """
            {
              "condition": { "scope": "product", "productId": 10, "quantity": 3 },
              "pricing": { "fixedPrice": 25 }
            }
            """);

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                new EvaluateCartLine { ProductId = "10", Quantity = 3, PricePerUnit = 10m },
            ],
            CartTotal = 30m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, Defaults, DateTime.UtcNow);

        Assert.Single(result.PromotionsApplied);
        var applied = result.PromotionsApplied[0];
        Assert.NotNull(applied.EligibleItems);
        Assert.Contains(applied.EligibleItems!, ei => ei.ProductId == "10" && ei.DiscountAmount > 0m);
        Assert.Contains("10", applied.TriggerProductIds!);
    }

    [Fact]
    public void CatalogBadge_BxGY_buy_all_does_not_badge_whole_catalog()
    {
        var promo = new Promotion
        {
            Id = 8,
            PromotionType = "buy_x_get_y",
            Name = "Buy any get gift",
            PayloadJson = """
                {
                  "condition": { "productScope": "all", "minQuantity": 2 },
                  "reward": { "benefitAppliesTo": "products", "productIds": [99], "discountType": "free" }
                }
                """,
            IsActive = true,
            IsDraft = false,
            IsDeleted = false,
            ShowBadge = true,
            ChannelsJson = "[\"all\"]",
        };

        var rules = PromotionCatalogBadgeResolver.ResolveRules([promo], "store", DateTime.UtcNow);

        Assert.Single(rules);
        Assert.False(rules[0].AllProducts);
        Assert.Contains(99, rules[0].ProductIds);
    }
}
