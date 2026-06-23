/*
  Backfill PromotionOrderRedemption (and rebuild PromotionDailyMetric from it) from existing
  stamped OrderItem rows. Run once after PromotionOrderRedemption_CreateTable.sql when migrating
  to the redemption-anchored metrics model.

  - One PromotionOrderRedemption row per (SiteId, OrderId, PromotionId), Source='order'.
  - DiscountAmount / RevenueNis summed across the order's lines for that promotion.
  - Channel mapped from Order.Source (kiosk->store, phone/manual->phone, woo/web->web).
  - PromotionDailyMetric is then rebuilt by aggregating the redemption rows, so both tables
    agree with the runtime logic in PromotionStorage.

  Safe to re-run: clears and rebuilds both tables.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.PromotionOrderRedemption', N'U') IS NULL
BEGIN
    RAISERROR(N'Run PromotionOrderRedemption_CreateTable.sql first.', 16, 1);
    RETURN;
END;

DELETE FROM dbo.PromotionOrderRedemption;

INSERT INTO dbo.PromotionOrderRedemption
    (SiteId, OrderId, ExternalOrderId, PromotionId, DiscountAmount, RevenueNis, Channel, RedeemedAtUtc, RecordedAtUtc, Source)
SELECT
    o.SiteId,
    o.Id AS OrderId,
    o.ExternalOrderId,
    oi.PromotionId,
    SUM(ISNULL(oi.DiscountAmount, 0))                              AS DiscountAmount,
    SUM(ISNULL(oi.TotalPrice, 0) - ISNULL(oi.DiscountAmount, 0))   AS RevenueNis,
    CASE LOWER(LTRIM(RTRIM(ISNULL(o.Source, N''))))
        WHEN N'kiosk' THEN N'store'
        WHEN N'phone' THEN N'phone'
        WHEN N'manual' THEN N'phone'
        WHEN N'mobile' THEN N'mobile'
        WHEN N'app' THEN N'mobile'
        WHEN N'woocommerce' THEN N'web'
        WHEN N'website' THEN N'web'
        WHEN N'web' THEN N'web'
        ELSE N'web'
    END AS Channel,
    o.CreationTime AS RedeemedAtUtc,
    SYSUTCDATETIME() AS RecordedAtUtc,
    N'order' AS Source
FROM dbo.OrderItem oi
INNER JOIN dbo.[Order] o ON o.Id = oi.OrderId
WHERE oi.PromotionId IS NOT NULL
  AND oi.IsDeleted = 0
  AND o.IsDeleted = 0
  AND o.Status <> N'Cancelled'
GROUP BY o.SiteId, o.Id, o.ExternalOrderId, oi.PromotionId,
    CASE LOWER(LTRIM(RTRIM(ISNULL(o.Source, N''))))
        WHEN N'kiosk' THEN N'store'
        WHEN N'phone' THEN N'phone'
        WHEN N'manual' THEN N'phone'
        WHEN N'mobile' THEN N'mobile'
        WHEN N'app' THEN N'mobile'
        WHEN N'woocommerce' THEN N'web'
        WHEN N'website' THEN N'web'
        WHEN N'web' THEN N'web'
        ELSE N'web'
    END,
    o.CreationTime;

PRINT N'PromotionOrderRedemption rows inserted: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));

-- Rebuild the daily KPI table from the redemption rows (one redemption = one order+promotion).
DELETE FROM dbo.PromotionDailyMetric;

INSERT INTO dbo.PromotionDailyMetric (PromotionId, MetricDateUtc, Channel, RedemptionsCount, RevenueNis, DiscountNis)
SELECT
    PromotionId,
    CAST(CAST(RedeemedAtUtc AS DATE) AS DATETIME) AS MetricDateUtc,
    Channel,
    COUNT(*)               AS RedemptionsCount,
    SUM(RevenueNis)        AS RevenueNis,
    SUM(DiscountAmount)    AS DiscountNis
FROM dbo.PromotionOrderRedemption
GROUP BY PromotionId, CAST(CAST(RedeemedAtUtc AS DATE) AS DATETIME), Channel;

PRINT N'PromotionDailyMetric rows rebuilt: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));
