-- Migration: Add default low-stock threshold columns to Account table
-- These columns store the account-level low-stock lines used for coloring and filtering on My Products.
-- DefaultLowStockThresholdWeighted: threshold for kg/weighted-style products
-- DefaultLowStockThresholdUnits:    threshold for unit-count products
-- When NULL the frontend falls back to hardcoded defaults (5 units / 5 kg).

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account'
      AND COLUMN_NAME = 'DefaultLowStockThresholdWeighted'
)
BEGIN
    ALTER TABLE Account
    ADD DefaultLowStockThresholdWeighted DECIMAL(18, 4) NULL;
END;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account'
      AND COLUMN_NAME = 'DefaultLowStockThresholdUnits'
)
BEGIN
    ALTER TABLE Account
    ADD DefaultLowStockThresholdUnits DECIMAL(18, 4) NULL;
END;
