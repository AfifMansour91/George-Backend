-- OCWSU "נמכר לפי" label (WooCommerce display_price_per_fixed_unit_label).
-- Run against the George database before deploying the backend update.

IF COL_LENGTH('WeightConfig', 'SoldByLabel') IS NULL
BEGIN
    ALTER TABLE [WeightConfig] ADD [SoldByLabel] NVARCHAR(32) NULL;
END
GO
