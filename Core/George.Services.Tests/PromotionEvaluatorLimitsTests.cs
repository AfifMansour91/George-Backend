using George.DB;
using George.Services;
using George.Services.Request;

namespace George.Services.Tests;

public class PromotionEvaluatorLimitsTests
{
    private static readonly PromotionEvaluator.SiteEvaluationDefaults Defaults = new();

    private static Promotion Promo(int id, string type, string payloadJson, string? discountKind = null) => new()
    {
        Id = id,
        PromotionType = type,
        ListDiscountKind = discountKind,
        PayloadJson = payloadJson,
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
}
