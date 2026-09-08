-- Adds OrderItem.DepreciationPercent: the פחת (%) the picker applied to a weighed line.
-- TotalPrice already includes it; the column lets invoices and the UI explain why
-- TotalPrice ÷ PickedQuantity is above the catalog ₪/kg (Dagei Gat order 7710: 3.21 kg × ₪120 × 1.25
-- printed as ₪150/kg on the Cardcom invoice with no explanation).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.OrderItem', N'DepreciationPercent') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrderItem] ADD [DepreciationPercent] DECIMAL(5, 2) NULL;
    PRINT 'Added DepreciationPercent to OrderItem';
END
GO
