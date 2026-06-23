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
