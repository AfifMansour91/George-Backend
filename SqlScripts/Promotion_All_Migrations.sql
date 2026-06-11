-- =============================================================================
-- Sprint 4 — Promotions feature: all migrations consolidated, ordered, idempotent.
-- Run against the George DB. Safe to re-run: every block is guarded.
--
-- Order of operations:
--   1. Promotion (base table)
--   2. Promotion list columns + PromotionDailyMetric table
--   3. Promotion (SiteId, CouponCode) unique index   [optional]
--   4. Site: promotion-default settings              (overage policy, phone-orders, discounted-products)
--   5. Site: promotion webhook URL + secret
--   6. OrderItem: PromotionId + DiscountAmount       (order-finalize linkage)
--
-- Spec: Sprint4/מבצעים.md
-- =============================================================================


-- -----------------------------------------------------------------------------
-- 1. Promotion (base table)
--    Source: SqlScripts/Promotion_CreateTable.sql
-- -----------------------------------------------------------------------------
IF OBJECT_ID(N'dbo.Promotion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Promotion (
        Id                  INT              IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
        IsDeleted           BIT              NOT NULL CONSTRAINT DF_Promotion_IsDeleted DEFAULT (0),
        GuidId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Promotion_GuidId DEFAULT (NEWID()),
        CreationTime        DATETIME2(0)     NOT NULL CONSTRAINT DF_Promotion_CreationTime DEFAULT (SYSUTCDATETIME()),
        UpdatedDate         DATETIME2(0)     NULL,
        CreationUserId      INT              NULL,
        UpdateUserId        INT              NULL,
        SiteId              INT              NOT NULL,
        PromotionType       NVARCHAR(40)     NOT NULL,
        Name                NVARCHAR(500)    NOT NULL,
        IsActive            BIT              NOT NULL CONSTRAINT DF_Promotion_IsActive DEFAULT (1),
        ShowBadge           BIT              NOT NULL CONSTRAINT DF_Promotion_ShowBadge DEFAULT (0),
        IsDraft             BIT              NOT NULL CONSTRAINT DF_Promotion_IsDraft DEFAULT (1),
        ScheduleStartDateUtc DATETIME2(0)    NULL,
        ScheduleEndDateUtc   DATETIME2(0)    NULL,
        PayloadJson         NVARCHAR(MAX)    NOT NULL CONSTRAINT DF_Promotion_PayloadJson DEFAULT ('{}'),
        CONSTRAINT FK_Promotion_Site FOREIGN KEY (SiteId) REFERENCES dbo.Site (Id)
    );

    CREATE NONCLUSTERED INDEX IX_Promotion_SiteId_IsDeleted
        ON dbo.Promotion (SiteId, IsDeleted)
        WHERE IsDeleted = 0;
END
GO


-- -----------------------------------------------------------------------------
-- 2. Promotion list columns + PromotionDailyMetric
--    Source: SqlScripts/Promotion_MetricsAndListColumns.sql
-- -----------------------------------------------------------------------------
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
        Channel            NVARCHAR(20)   NOT NULL CONSTRAINT DF_PromotionDailyMetric_Channel DEFAULT (N'web'),
        RedemptionsCount   INT            NOT NULL CONSTRAINT DF_PromotionDailyMetric_Redemptions DEFAULT (0),
        RevenueNis         DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PromotionDailyMetric_Revenue DEFAULT (0),
        DiscountNis        DECIMAL(18,2)  NOT NULL CONSTRAINT DF_PromotionDailyMetric_Discount DEFAULT (0),
        CONSTRAINT FK_PromotionDailyMetric_Promotion FOREIGN KEY (PromotionId) REFERENCES dbo.Promotion (Id) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_PromotionDailyMetric_Promotion_Date_Channel
        ON dbo.PromotionDailyMetric (PromotionId, MetricDateUtc, Channel);
END
GO

-- Upgrade existing PromotionDailyMetric (pre-channel installs)
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


-- -----------------------------------------------------------------------------
-- 3. Promotion (SiteId, CouponCode) unique index   [optional but recommended]
--    Source: SqlScripts/Promotion_CouponUniqueIndex.sql
-- -----------------------------------------------------------------------------
-- Filtered-index WHERE must use simple comparisons only (no LTRIM/RTRIM — SQL Server error 10735).
-- Whitespace normalization is enforced in PromotionService on create/update.
IF EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Promotion_SiteId_CouponCode'
      AND object_id = OBJECT_ID(N'dbo.Promotion', N'U')
)
BEGIN
    DROP INDEX UX_Promotion_SiteId_CouponCode ON dbo.Promotion;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Promotion_SiteId_CouponCode'
      AND object_id = OBJECT_ID(N'dbo.Promotion', N'U')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Promotion_SiteId_CouponCode
        ON dbo.Promotion (SiteId, CouponCode)
        WHERE CouponCode IS NOT NULL AND CouponCode <> N'' AND IsDeleted = 0;
END
GO


-- -----------------------------------------------------------------------------
-- 4. Site: per-site promotion default settings
--    Source: SqlScripts/Site_AddPromotionSettings.sql
--    Spec section: "הגדרות מבצעים (תחת הגדרות חנות תחת מבצעים)"
-- -----------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Site', N'PromotionOveragePolicyDefault') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionOveragePolicyDefault NVARCHAR(20) NULL
            CONSTRAINT DF_Site_PromotionOveragePolicyDefault DEFAULT (N'full_price');
END
GO

IF COL_LENGTH(N'dbo.Site', N'PromotionsApplyToPhoneOrders') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionsApplyToPhoneOrders BIT NULL
            CONSTRAINT DF_Site_PromotionsApplyToPhoneOrders DEFAULT (1);
END
GO

IF COL_LENGTH(N'dbo.Site', N'PromotionsApplyToDiscountedProducts') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionsApplyToDiscountedProducts BIT NULL
            CONSTRAINT DF_Site_PromotionsApplyToDiscountedProducts DEFAULT (0);
END
GO


-- -----------------------------------------------------------------------------
-- 5. Site: promotion webhook URL + signing secret
--    Source: SqlScripts/Site_AddPromotionWebhook.sql
--    Spec section: "סנכרון מבצעים לאתר ולקיוסק (Webhook)"
-- -----------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Site', N'PromotionWebhookUrl') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionWebhookUrl NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PromotionWebhookSecret') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        PromotionWebhookSecret NVARCHAR(200) NULL;
END
GO


-- -----------------------------------------------------------------------------
-- 6. OrderItem: link order lines to the promotion that discounted them
--    Source: SqlScripts/OrderItem_AddPromotionLinkage.sql
--    Spec section: "סיכום אחריות"
-- -----------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.OrderItem', N'PromotionId') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD
        PromotionId INT NULL;
END
GO

IF COL_LENGTH(N'dbo.OrderItem', N'DiscountAmount') IS NULL
BEGIN
    ALTER TABLE dbo.OrderItem ADD
        DiscountAmount DECIMAL(18, 2) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderItem_PromotionId' AND object_id = OBJECT_ID(N'dbo.OrderItem'))
BEGIN
    CREATE INDEX IX_OrderItem_PromotionId
        ON dbo.OrderItem(PromotionId)
        WHERE PromotionId IS NOT NULL;
END
GO


-- =============================================================================
-- Optional after deploy (channel + net revenue semantics):
--   SqlScripts/Promotion_MetricsChannel.sql  (if upgrading an older DB)
--   SqlScripts/Promotion_MetricsBackfill.sql
--
-- Done. Verify with:
--
--   SELECT name FROM sys.tables  WHERE name IN (N'Promotion', N'PromotionDailyMetric');
--   SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Site')
--                                  AND name LIKE N'Promotion%';
--   SELECT name FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem')
--                                  AND name IN (N'PromotionId', N'DiscountAmount');
--   SELECT name FROM sys.indexes WHERE name LIKE N'%Promotion%' OR name = N'IX_OrderItem_PromotionId';
-- =============================================================================
