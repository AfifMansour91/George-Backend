using George.Data;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace George.Services.Tests;

/// <summary>
/// Idempotency + dedup tests for the redemption-anchored promotion metrics
/// (<see cref="PromotionStorage.RecordOrderPromotionRedemptionsAsync"/> and the external
/// /redemptions dedup). Backs the website-order double-count fix:
/// shop-manager/docs/wooCommerceEngines/ORDER_PROMOTION_SYNC_SPEC.md.
/// </summary>
public class PromotionRedemptionIdempotencyTests
{
    private const int SiteId = 1;
    private const int PromoId = 1;
    private static readonly DateTime RedeemedAt = new(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

    private static GeorgeDBContext NewContext()
    {
        var options = new DbContextOptionsBuilder<GeorgeDBContextBase>()
            .UseInMemoryDatabase($"promo-redemption-{Guid.NewGuid()}")
            .EnableServiceProviderCaching(false)
            .Options;
        return new GeorgeDBContext(options);
    }

    private static PromotionStorage NewStorage(GeorgeDBContext ctx) =>
        new(ctx, NullLogger<PromotionStorage>.Instance);

    private static void SeedPromotion(GeorgeDBContext ctx, int id = PromoId, int siteId = SiteId)
    {
        ctx.Promotion.Add(new Promotion
        {
            Id = id,
            SiteId = siteId,
            Name = $"Promo {id}",
            PromotionType = "discount",
            IsActive = true,
            IsDraft = false,
            IsDeleted = false,
        });
        ctx.SaveChanges();
    }

    private static void SeedOrder(GeorgeDBContext ctx, int id, string externalOrderId, int siteId = SiteId)
    {
        ctx.Order.Add(new Order
        {
            Id = id,
            SiteId = siteId,
            ExternalOrderId = externalOrderId,
            OrderNumber = externalOrderId,
            Source = "WooCommerce",
            Status = "Completed",
            PaymentStatus = "Paid",
            IsDeleted = false,
            CreationTime = RedeemedAt,
        });
        ctx.SaveChanges();
    }

    private static IReadOnlyList<(int PromotionId, decimal DiscountAmount, decimal RevenueNis)> Rows(
        int promotionId = PromoId, decimal discount = 10m, decimal revenue = 90m) =>
        new[] { (promotionId, discount, revenue) };

    [Fact]
    public async Task Record_same_order_twice_counts_once()
    {
        using var ctx = NewContext();
        var storage = NewStorage(ctx);

        await storage.RecordOrderPromotionRedemptionsAsync(SiteId, 100, "100", "web", RedeemedAt, Rows(), default);
        await storage.RecordOrderPromotionRedemptionsAsync(SiteId, 100, "100", "web", RedeemedAt, Rows(), default);

        Assert.Equal(1, await ctx.PromotionOrderRedemption.CountAsync(r => r.OrderId == 100));
        var metric = await ctx.PromotionDailyMetric.SingleAsync(m => m.PromotionId == PromoId);
        Assert.Equal(1, metric.RedemptionsCount);
        Assert.Equal(10m, metric.DiscountNis);
        Assert.Equal(90m, metric.RevenueNis);
    }

    [Fact]
    public async Task Reverse_removes_rows_and_zeroes_metric()
    {
        using var ctx = NewContext();
        var storage = NewStorage(ctx);

        await storage.RecordOrderPromotionRedemptionsAsync(SiteId, 100, "100", "web", RedeemedAt, Rows(), default);
        await storage.ReverseOrderPromotionRedemptionsAsync(SiteId, 100, default);

        Assert.Equal(0, await ctx.PromotionOrderRedemption.CountAsync(r => r.OrderId == 100));
        var metric = await ctx.PromotionDailyMetric.SingleAsync(m => m.PromotionId == PromoId);
        Assert.Equal(0, metric.RedemptionsCount);
        Assert.Equal(0m, metric.DiscountNis);
        Assert.Equal(0m, metric.RevenueNis);
    }

    [Fact]
    public async Task Distinct_orders_same_promotion_count_separately()
    {
        using var ctx = NewContext();
        var storage = NewStorage(ctx);

        await storage.RecordOrderPromotionRedemptionsAsync(SiteId, 100, "100", "web", RedeemedAt, Rows(), default);
        await storage.RecordOrderPromotionRedemptionsAsync(SiteId, 101, "101", "web", RedeemedAt, Rows(), default);

        var metric = await ctx.PromotionDailyMetric.SingleAsync(m => m.PromotionId == PromoId);
        Assert.Equal(2, metric.RedemptionsCount);
        Assert.Equal(20m, metric.DiscountNis);
    }

    [Fact]
    public async Task External_redemption_is_skipped_when_order_already_counted()
    {
        using var ctx = NewContext();
        SeedPromotion(ctx);
        SeedOrder(ctx, id: 100, externalOrderId: "555");
        var storage = NewStorage(ctx);

        // Order sync counted it first (Source=order).
        await storage.RecordOrderPromotionRedemptionsAsync(SiteId, 100, "555", "web", RedeemedAt, Rows(), default);

        // The Promeng /redemptions report for the same Woo order id must NOT double-count.
        var recorded = await storage.RecordExternalRedemptionsAsync(
            SiteId,
            new[] { (PromoId, 10m, RedeemedAt, "web", (string?)"555") },
            default);

        Assert.Equal(0, recorded);
        var metric = await ctx.PromotionDailyMetric.SingleAsync(m => m.PromotionId == PromoId);
        Assert.Equal(1, metric.RedemptionsCount);
        Assert.Equal(10m, metric.DiscountNis);
    }

    [Fact]
    public async Task External_redemption_counts_when_no_matching_order()
    {
        using var ctx = NewContext();
        SeedPromotion(ctx);
        var storage = NewStorage(ctx);

        // No George order with this external id (store using Promeng without order sync) - legacy aggregate.
        var recorded = await storage.RecordExternalRedemptionsAsync(
            SiteId,
            new[] { (PromoId, 5m, RedeemedAt, "web", (string?)"999") },
            default);

        Assert.Equal(1, recorded);
        var metric = await ctx.PromotionDailyMetric.SingleAsync(m => m.PromotionId == PromoId);
        Assert.Equal(1, metric.RedemptionsCount);
        Assert.Equal(5m, metric.DiscountNis);
    }
}
