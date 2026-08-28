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
