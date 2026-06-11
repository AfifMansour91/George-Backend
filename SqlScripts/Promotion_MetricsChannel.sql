-- Add order-source channel to PromotionDailyMetric for KPI filtering by order channel.
-- Run after Promotion_MetricsAndListColumns.sql. Safe to re-run (idempotent).

IF COL_LENGTH(N'dbo.PromotionDailyMetric', N'Channel') IS NULL
BEGIN
    ALTER TABLE dbo.PromotionDailyMetric ADD
        Channel NVARCHAR(20) NOT NULL CONSTRAINT DF_PromotionDailyMetric_Channel DEFAULT (N'web');
END
GO

IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PromotionDailyMetric_Promotion_Date'
      AND object_id = OBJECT_ID(N'dbo.PromotionDailyMetric')
)
BEGIN
    DROP INDEX UX_PromotionDailyMetric_Promotion_Date ON dbo.PromotionDailyMetric;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_PromotionDailyMetric_Promotion_Date_Channel'
      AND object_id = OBJECT_ID(N'dbo.PromotionDailyMetric')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_PromotionDailyMetric_Promotion_Date_Channel
        ON dbo.PromotionDailyMetric (PromotionId, MetricDateUtc, Channel);
END
GO

PRINT N'Promotion_MetricsChannel completed. Re-run Promotion_MetricsBackfill.sql to rebuild aggregates with channel + net revenue.';
