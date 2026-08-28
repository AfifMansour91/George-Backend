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
