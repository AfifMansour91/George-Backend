-- =============================================================================
-- PROMOTIONS FEATURE - FULL PRODUCTION INSTALL (transaction-wrapped, idempotent).
-- Run once on a DB that has nothing promotion-related yet. Safe to re-run: every
-- block is guarded. Requires dbo.Site, dbo.[Order], dbo.OrderItem to exist.
--
-- ATOMIC: the whole install runs inside one transaction. If any step fails,
-- XACT_ABORT rolls the transaction back and the "IF XACT_STATE() = 0 SET NOEXEC ON"
-- guards stop every later step from applying - so you never get a partial install.
--   * Recommended run: sqlcmd -b -i Promotions_Prod_Install.sql   (also fine in SSMS).
--   * If a run DID error: the changes were rolled back. The session may be left with
--     NOEXEC ON - just run "SET NOEXEC OFF;" (or open a new window) before retrying.
--
-- Run order (do not reorder):
--   1. Promotion base table              2. list columns + PromotionDailyMetric
--   3. metric Channel + unique index     4. Promotion.Priority
--   5. coupon unique index               6. Site promotion settings
--   7. Site promotion webhook            8. OrderItem promotion linkage
--   9. PromotionOrderRedemption table   10. backfill (no-op on a fresh DB)
--
-- Spec: shop-manager/docs/wooCommerceEngines/ORDER_PROMOTION_SYNC_SPEC.md
--        Sprint4/מבצעים.md
-- =============================================================================

SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

BEGIN TRANSACTION;
GO


-- =============================================================================
-- SOURCE: Promotion_CreateTable.sql
-- =============================================================================

-- Promotions (Sprint 4): run against the George DB.
IF OBJECT_ID(N'dbo.Promotion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Promotion (
        Id                  INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
        IsDeleted           BIT            NOT NULL CONSTRAINT DF_Promotion_IsDeleted DEFAULT (0),
        GuidId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Promotion_GuidId DEFAULT (NEWID()),
        CreationTime        DATETIME2(0)   NOT NULL CONSTRAINT DF_Promotion_CreationTime DEFAULT (SYSUTCDATETIME()),
        UpdatedDate         DATETIME2(0)   NULL,
        CreationUserId      INT            NULL,
        UpdateUserId        INT            NULL,
        SiteId              INT            NOT NULL,
        PromotionType       NVARCHAR(40)   NOT NULL,
        Name                NVARCHAR(500)  NOT NULL,
        IsActive            BIT            NOT NULL CONSTRAINT DF_Promotion_IsActive DEFAULT (1),
        ShowBadge           BIT            NOT NULL CONSTRAINT DF_Promotion_ShowBadge DEFAULT (0),
        IsDraft             BIT            NOT NULL CONSTRAINT DF_Promotion_IsDraft DEFAULT (1),
        ScheduleStartDateUtc DATETIME2(0)  NULL,
        ScheduleEndDateUtc   DATETIME2(0)  NULL,
        PayloadJson         NVARCHAR(MAX)  NOT NULL CONSTRAINT DF_Promotion_PayloadJson DEFAULT ('{}'),
        CONSTRAINT FK_Promotion_Site FOREIGN KEY (SiteId) REFERENCES dbo.Site (Id)
    );

    CREATE NONCLUSTERED INDEX IX_Promotion_SiteId_IsDeleted
        ON dbo.Promotion (SiteId, IsDeleted)
        WHERE IsDeleted = 0;
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Promotion_MetricsAndListColumns.sql
-- =============================================================================

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

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Promotion_MetricsChannel.sql
-- =============================================================================

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

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Promotion_AddPriority.sql
-- =============================================================================

-- Promotion priority for evaluation order (lower = first). Idempotent.
IF COL_LENGTH('dbo.Promotion', 'Priority') IS NULL
BEGIN
    ALTER TABLE dbo.Promotion
        ADD Priority INT NOT NULL CONSTRAINT DF_Promotion_Priority DEFAULT (10);
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Promotion_CouponUniqueIndex.sql
-- =============================================================================

-- Optional: enforce unique coupon code per site (non-deleted rows with a code).
-- Run after Promotion_MetricsAndListColumns.sql if you want DB-level uniqueness in addition to API checks.

-- Filtered-index WHERE must use simple comparisons only (no LTRIM/RTRIM - SQL Server error 10735).
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

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Site_AddPromotionSettings.sql
-- =============================================================================

-- Sprint 4: per-site promotion settings.
-- Spec source: `Sprint4/מבצעים.md` - "הגדרות מבצעים (תחת הגדרות חנות תחת מבצעים)".
-- Idempotent: each ALTER guarded by COL_LENGTH check so the script can be re-run safely.

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

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: Site_AddPromotionWebhook.sql
-- =============================================================================

-- Sprint 4: per-site promotion webhook URL + signing secret.
-- Spec: `Sprint4/מבצעים.md` - "סנכרון מבצעים לאתר ולקיוסק (Webhook)".
-- Idempotent: each ALTER guarded so the script can be re-run safely.

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

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: OrderItem_AddPromotionLinkage.sql
-- =============================================================================

-- Sprint 4: link order lines to the promotion that discounted them.
-- Spec: `Sprint4/מבצעים.md` - "סיכום אחריות" (promotion impact must be persisted on the
-- order so reports + per_customer enforcement can read it later).
-- Idempotent: each ALTER guarded so the script can be re-run safely.

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

-- Helper index for "show me all the orders that used promotion X" reports.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_OrderItem_PromotionId' AND object_id = OBJECT_ID(N'dbo.OrderItem'))
BEGIN
    CREATE INDEX IX_OrderItem_PromotionId
        ON dbo.OrderItem(PromotionId)
        WHERE PromotionId IS NOT NULL;
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: PromotionOrderRedemption_CreateTable.sql
-- =============================================================================

-- Sprint 4 / website-order promotions: idempotency anchor for promotion redemptions.
-- One row per (SiteId, OrderId, PromotionId). A PromotionDailyMetric delta is applied only when
-- a row is newly inserted, so a re-sent order or a duplicate /redemptions report can't double-count.
-- ExternalOrderId is a helper column for matching external /redemptions reports back to the order.
-- Spec: shop-manager/docs/wooCommerceEngines/ORDER_PROMOTION_SYNC_SPEC.md
-- Idempotent: safe to re-run.

IF OBJECT_ID(N'dbo.PromotionOrderRedemption', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PromotionOrderRedemption
    (
        Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_PromotionOrderRedemption PRIMARY KEY,
        SiteId          INT            NOT NULL,
        OrderId         INT            NOT NULL,
        ExternalOrderId NVARCHAR(100)  NULL,
        PromotionId     INT            NOT NULL,
        DiscountAmount  DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PromotionOrderRedemption_DiscountAmount DEFAULT (0),
        RevenueNis      DECIMAL(18, 2) NOT NULL CONSTRAINT DF_PromotionOrderRedemption_RevenueNis DEFAULT (0),
        Channel         NVARCHAR(20)   NOT NULL CONSTRAINT DF_PromotionOrderRedemption_Channel DEFAULT (N'web'),
        RedeemedAtUtc   DATETIME2(0)   NOT NULL,
        RecordedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_PromotionOrderRedemption_RecordedAtUtc DEFAULT (sysutcdatetime()),
        Source          NVARCHAR(20)   NOT NULL CONSTRAINT DF_PromotionOrderRedemption_Source DEFAULT (N'order')
    );
END
GO

-- Dedup key: a promotion is counted once per order per site, regardless of which channel reported it.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_PromotionOrderRedemption_Site_Order_Promotion' AND object_id = OBJECT_ID(N'dbo.PromotionOrderRedemption'))
BEGIN
    CREATE UNIQUE INDEX UX_PromotionOrderRedemption_Site_Order_Promotion
        ON dbo.PromotionOrderRedemption(SiteId, OrderId, PromotionId);
END
GO

-- "All redemptions of promotion X" reads.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PromotionOrderRedemption_PromotionId' AND object_id = OBJECT_ID(N'dbo.PromotionOrderRedemption'))
BEGIN
    CREATE INDEX IX_PromotionOrderRedemption_PromotionId
        ON dbo.PromotionOrderRedemption(PromotionId);
END
GO

-- Match external /redemptions reports (which carry the WooCommerce order id) back to a row.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PromotionOrderRedemption_Site_ExternalOrder' AND object_id = OBJECT_ID(N'dbo.PromotionOrderRedemption'))
BEGIN
    CREATE INDEX IX_PromotionOrderRedemption_Site_ExternalOrder
        ON dbo.PromotionOrderRedemption(SiteId, ExternalOrderId);
END
GO

-- Referential integrity to Promotion (cascade so deleting a promotion clears its redemptions).
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PromotionOrderRedemption_Promotion')
BEGIN
    ALTER TABLE dbo.PromotionOrderRedemption WITH CHECK
        ADD CONSTRAINT FK_PromotionOrderRedemption_Promotion
        FOREIGN KEY (PromotionId) REFERENCES dbo.Promotion(Id) ON DELETE CASCADE;
END
GO

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO


-- =============================================================================
-- SOURCE: PromotionOrderRedemption_Backfill.sql
-- =============================================================================

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

GO
IF XACT_STATE() = 0 SET NOEXEC ON;  -- a prior step failed + rolled back; skip the rest
GO

-- =============================================================================
-- COMMIT
-- =============================================================================
IF @@TRANCOUNT > 0 COMMIT TRANSACTION;
SET NOEXEC OFF;
PRINT N'Promotions production install committed successfully.';
GO
