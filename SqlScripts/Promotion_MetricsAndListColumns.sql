-- Sprint 4+: list columns + daily aggregates for promotion KPIs and server-side metrics.
-- Run against the George DB after Promotion_CreateTable.sql.

IF COL_LENGTH(N'dbo.Promotion', N'ListDiscountKind') IS NULL
BEGIN
    ALTER TABLE dbo.Promotion ADD
        ListDiscountKind NVARCHAR(20) NOT NULL CONSTRAINT DF_Promotion_ListDiscountKind DEFAULT (N'percent');
END
GO

IF COL_LENGTH(N'dbo.Promotion', N'ChannelsJson') IS NULL
BEGIN
    ALTER TABLE dbo.Promotion ADD
        ChannelsJson NVARCHAR(500) NULL CONSTRAINT DF_Promotion_ChannelsJson DEFAULT (N'["web"]');
END
GO

IF COL_LENGTH(N'dbo.Promotion', N'CouponCode') IS NULL
BEGIN
    ALTER TABLE dbo.Promotion ADD CouponCode NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH(N'dbo.Promotion', N'AppliesToSummary') IS NULL
BEGIN
    ALTER TABLE dbo.Promotion ADD AppliesToSummary NVARCHAR(500) NULL;
END
GO

IF OBJECT_ID(N'dbo.PromotionDailyMetric', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PromotionDailyMetric (
        Id                 INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_PromotionDailyMetric PRIMARY KEY,
        PromotionId        INT            NOT NULL,
        MetricDateUtc      DATE           NOT NULL,
        RedemptionsCount   INT            NOT NULL CONSTRAINT DF_PromotionDailyMetric_Redemptions DEFAULT (0),
        RevenueNis         DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PromotionDailyMetric_Revenue DEFAULT (0),
        DiscountNis        DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PromotionDailyMetric_Discount DEFAULT (0),
        CONSTRAINT FK_PromotionDailyMetric_Promotion FOREIGN KEY (PromotionId) REFERENCES dbo.Promotion (Id) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_PromotionDailyMetric_Promotion_Date
        ON dbo.PromotionDailyMetric (PromotionId, MetricDateUtc);
END
GO
