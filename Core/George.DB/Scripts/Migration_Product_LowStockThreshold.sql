-- Migration: Add per-product low-stock threshold column to Product table
-- When set (> 0), this value overrides the account-level DefaultLowStockThreshold* for this specific product.
-- NULL means "use account default".

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Product'
      AND COLUMN_NAME = 'LowStockThreshold'
)
BEGIN
    ALTER TABLE Product
    ADD LowStockThreshold DECIMAL(18, 4) NULL;
END;
