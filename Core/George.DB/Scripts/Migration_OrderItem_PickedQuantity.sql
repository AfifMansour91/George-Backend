-- Add PickedQuantity to OrderItem (Sprint 2: save picking state for שמור וצא).
-- Run this on the database so the entity column PickedQuantity exists.

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItem')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[OrderItem]') AND name = 'PickedQuantity')
    BEGIN
        ALTER TABLE [dbo].[OrderItem] ADD [PickedQuantity] [decimal](18, 4) NULL;
        PRINT 'Added OrderItem.PickedQuantity'
    END
END
GO
