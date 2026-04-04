-- Site catalog stock supports fractional quantities (e.g. kg) and picking deltas.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = N'StockQuantity')
BEGIN
    ALTER TABLE [dbo].[Product] ALTER COLUMN [StockQuantity] DECIMAL(18, 4) NULL;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[ProductVariant]') AND name = N'StockQuantity')
BEGIN
    ALTER TABLE [dbo].[ProductVariant] ALTER COLUMN [StockQuantity] DECIMAL(18, 4) NULL;
END
GO
