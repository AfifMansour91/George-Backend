using George.DB;
using George.Services;
using George.Services.Request;

namespace George.Services.Tests;

/// <summary>Bundles (מארזים) in promotions - BUNDLES_SYNC_SPEC.md §9.</summary>
public class PromotionBundleLinesTests
{
    private static OrderItem Plain(int productId) => new() { ProductId = productId, Quantity = 1m, TotalPrice = 10m };

    private static OrderItem Parent(int id, int bundleProductId, int? promotionId = null, decimal? discount = null) =>
        new() { Id = id, ProductId = bundleProductId, BundleProductId = bundleProductId, Quantity = 1m, TotalPrice = 199m, PromotionId = promotionId, DiscountAmount = discount };

    private static OrderItem Child(int parentId, int productId) =>
        new() { ProductId = productId, ParentOrderItemId = parentId, Quantity = 1m };

    [Fact]
    public void FilterEvaluable_DropsChildren_KeepsParentsAndPlainLines()
    {
        var lines = new List<OrderItem> { Plain(1), Parent(5, 10), Child(5, 2293), Child(5, 50) };

        var evaluable = PromotionBundleLines.FilterEvaluable(lines);

        Assert.Equal(2, evaluable.Count);
        Assert.Contains(evaluable, l => l.ProductId == 1);
        Assert.Contains(evaluable, l => l.ProductId == 10 && BundleOrderLines.IsBundleParent(l));
        Assert.DoesNotContain(evaluable, BundleOrderLines.IsBundleChild);
    }

    [Fact]
    public void IsDiscountedBundle_TrueUnlessDiscountTypeNone()
    {
        Assert.False(PromotionBundleLines.IsDiscountedBundle(null));
        Assert.False(PromotionBundleLines.IsDiscountedBundle(new ProductBundleConfig { DiscountType = "none" }));
        Assert.True(PromotionBundleLines.IsDiscountedBundle(new ProductBundleConfig { DiscountType = "percent", DiscountValue = 10m }));
        Assert.True(PromotionBundleLines.IsDiscountedBundle(new ProductBundleConfig { DiscountType = "fixed", DiscountValue = 20m }));
    }

    [Fact]
    public void IsWooSourcedOrder_RecognisesStorefrontSources()
    {
        Assert.True(PromotionBundleLines.IsWooSourcedOrder("WooCommerce"));
        Assert.True(PromotionBundleLines.IsWooSourcedOrder("website"));
        Assert.False(PromotionBundleLines.IsWooSourcedOrder("Phone"));
        Assert.False(PromotionBundleLines.IsWooSourcedOrder("Kiosk"));
        Assert.False(PromotionBundleLines.IsWooSourcedOrder(null));
    }

    [Fact]
    public void RevertNewStampsOnUnstampedBundleParents_RemovesOnlyNewParentStamps()
    {
        var stampedByWoo = Parent(1, 10, promotionId: 7, discount: 20m);
        var unstamped = Parent(2, 11);
        var plain = Plain(3);
        var lines = new List<OrderItem> { stampedByWoo, unstamped, plain };

        var before = PromotionBundleLines.SnapshotBundleParentStamps(lines);

        // Simulate the picking re-evaluation: wipe + re-stamp everything.
        stampedByWoo.PromotionId = 7; stampedByWoo.DiscountAmount = 25m;   // re-scaled - allowed
        unstamped.PromotionId = 7; unstamped.DiscountAmount = 30m;         // new stamp - not allowed
        plain.PromotionId = 7; plain.DiscountAmount = 1m;                  // plain line - untouched

        var reverted = PromotionBundleLines.RevertNewStampsOnUnstampedBundleParents(lines, before);

        Assert.Equal(1, reverted);
        Assert.Equal(7, stampedByWoo.PromotionId);
        Assert.Equal(25m, stampedByWoo.DiscountAmount);
        Assert.Null(unstamped.PromotionId);
        Assert.Null(unstamped.DiscountAmount);
        Assert.Equal(7, plain.PromotionId);
    }

    [Fact]
    public void RevertNewStampsOnUnstampedBundleParents_KeepsUnlinkedWooDiscount()
    {
        // A locally-authored Woo discount (no George link) on the parent survives the revert.
        var parent = Parent(1, 10, promotionId: null, discount: 15m);
        var lines = new List<OrderItem> { parent };
        var before = PromotionBundleLines.SnapshotBundleParentStamps(lines);

        parent.PromotionId = 9; parent.DiscountAmount = 15m + 12m;

        Assert.Equal(1, PromotionBundleLines.RevertNewStampsOnUnstampedBundleParents(lines, before));
        Assert.Null(parent.PromotionId);
        Assert.Equal(15m, parent.DiscountAmount);
    }

    [Fact]
    public void Evaluator_BundleParent_CountsOnePerBundleUnit_ByItsOwnCategory()
    {
        // "Buy 3 from category 5, pay 50" - the bundle product (category 5) with quantity 3 qualifies as 3 units.
        var promo = new Promotion
        {
            Id = 1,
            PromotionType = "buy_x_pay_y",
            PayloadJson = """
                {
                  "condition": { "scope": "category", "categoryId": 5, "quantity": 3 },
                  "pricing": { "fixedPrice": 500 }
                }
                """,
            IsActive = true,
            IsDraft = false,
            Name = "3 מארזים ב-500",
            ChannelsJson = "[\"all\"]",
        };

        var req = new EvaluatePromotionsReq
        {
            SiteId = 1,
            Cart =
            [
                // Bundle parent as the order path builds it: bundle product id, its categories, qty = bundles.
                new EvaluateCartLine { ProductId = "10", Quantity = 3, PricePerUnit = 199m, CategoryId = "5" },
            ],
            CartTotal = 597m,
        };

        var result = PromotionEvaluator.Evaluate([promo], req, new PromotionEvaluator.SiteEvaluationDefaults(), DateTime.UtcNow);

        var applied = Assert.Single(result.PromotionsApplied);
        Assert.Equal(97m, applied.DiscountAmount);
    }
}
