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
