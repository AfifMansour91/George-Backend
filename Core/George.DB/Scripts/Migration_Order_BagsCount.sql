-- Add BagsCount to Order (Sprint 2: number of bags/cartons at end of picking).
-- Run this on the database so the entity column BagsCount exists.

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Order')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = 'BagsCount')
    BEGIN
        ALTER TABLE [dbo].[Order] ADD [BagsCount] [int] NULL;
        PRINT 'Added Order.BagsCount'
    END
END
GO
