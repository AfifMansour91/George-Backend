-- When StockManagementType is variation: 1 = track quantity per variation in Woo; 0/NULL = in/out only per variation.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = 'VariationStockByQuantity'
)
BEGIN
    ALTER TABLE [dbo].[Product] ADD [VariationStockByQuantity] BIT NULL;
END
GO
