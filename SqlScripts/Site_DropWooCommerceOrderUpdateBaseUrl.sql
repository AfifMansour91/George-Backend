-- Run on George DB after deploying API that no longer maps WooCommerceOrderUpdateBaseUrl (oc-storeos uses WooCommerceUrl).
IF COL_LENGTH('dbo.Site', 'WooCommerceOrderUpdateBaseUrl') IS NOT NULL
    ALTER TABLE dbo.Site DROP COLUMN WooCommerceOrderUpdateBaseUrl;
