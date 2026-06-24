-- Customer KPI refresh: per-site customer-behavior setting.
-- "A customer is considered inactive / at-risk-of-churn after N days without an order"
-- (single-order customers; customers with history use avg-gap × 2). Default 14.
-- Idempotent: ALTER guarded by COL_LENGTH check so the script can be re-run safely.

IF COL_LENGTH(N'dbo.Site', N'CustomerInactiveAfterDays') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD
        CustomerInactiveAfterDays INT NULL
            CONSTRAINT DF_Site_CustomerInactiveAfterDays DEFAULT (14);
END
GO
