-- Optional: enforce unique coupon code per site (non-deleted rows with a code).
-- Run after Promotion_MetricsAndListColumns.sql if you want DB-level uniqueness in addition to API checks.

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_Promotion_SiteId_CouponCode'
      AND object_id = OBJECT_ID(N'dbo.Promotion', N'U')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_Promotion_SiteId_CouponCode
        ON dbo.Promotion (SiteId, CouponCode)
        WHERE CouponCode IS NOT NULL AND LTRIM(RTRIM(CouponCode)) <> N'' AND IsDeleted = 0;
END
GO
