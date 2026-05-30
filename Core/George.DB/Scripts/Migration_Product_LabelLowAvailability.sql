-- Storefront label: זמינות נמוכה (low_availability) → Woo ed/v1 product-low-availability
IF COL_LENGTH('dbo.Product', 'LabelLowAvailability') IS NULL
    ALTER TABLE dbo.Product ADD LabelLowAvailability bit NOT NULL CONSTRAINT DF_Product_LabelLowAvailability DEFAULT (0);
