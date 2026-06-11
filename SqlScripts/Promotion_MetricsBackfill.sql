/*
  Rebuild PromotionDailyMetric from OrderItem rows with PromotionId.
  - Channel: mapped from Order.Source (kiosk→store, phone/manual→phone, woo/web→web).
  - RevenueNis: net line total (TotalPrice − DiscountAmount).
  - DiscountNis: sum of DiscountAmount on promoted lines.
  Requires Promotion_MetricsChannel.sql (Channel column + unique index).

  Safe to re-run: replaces all rows in PromotionDailyMetric.
*/
SET NOCOUNT ON;

IF OBJECT_ID(N'dbo.PromotionDailyMetric', N'U') IS NULL
BEGIN
    RAISERROR(N'PromotionDailyMetric table does not exist.', 16, 1);
    RETURN;
END;

IF COL_LENGTH(N'dbo.PromotionDailyMetric', N'Channel') IS NULL
BEGIN
    RAISERROR(N'Run Promotion_MetricsChannel.sql first.', 16, 1);
    RETURN;
END;

DELETE FROM dbo.PromotionDailyMetric;

;WITH LineAgg AS (
    SELECT
        oi.PromotionId,
        CAST(CAST(o.CreationTime AS DATE) AS DATETIME) AS MetricDateUtc,
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
        o.Id AS OrderId,
        SUM(ISNULL(oi.TotalPrice, 0) - ISNULL(oi.DiscountAmount, 0)) AS RevenueNis,
        SUM(ISNULL(oi.DiscountAmount, 0)) AS DiscountNis
    FROM dbo.OrderItem oi
    INNER JOIN dbo.[Order] o ON o.Id = oi.OrderId
    WHERE oi.PromotionId IS NOT NULL
      AND o.IsDeleted = 0
      AND o.Status <> N'Cancelled'
    GROUP BY
        oi.PromotionId,
        CAST(CAST(o.CreationTime AS DATE) AS DATETIME),
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
        o.Id
),
DailyAgg AS (
    SELECT
        PromotionId,
        MetricDateUtc,
        Channel,
        COUNT(*) AS RedemptionsCount,
        SUM(RevenueNis) AS RevenueNis,
        SUM(DiscountNis) AS DiscountNis
    FROM LineAgg
    GROUP BY PromotionId, MetricDateUtc, Channel
)
INSERT INTO dbo.PromotionDailyMetric (PromotionId, MetricDateUtc, Channel, RedemptionsCount, RevenueNis, DiscountNis)
SELECT PromotionId, MetricDateUtc, Channel, RedemptionsCount, RevenueNis, DiscountNis
FROM DailyAgg;

PRINT N'Promotion_MetricsBackfill completed. Rows inserted: ' + CAST(@@ROWCOUNT AS NVARCHAR(20));
