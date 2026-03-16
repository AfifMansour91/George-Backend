-- Migration: Add soft delete (and UpdatedDate) to OrderItem (like Order entity).
-- Run this script on your database before deploying the application change.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = N'OrderItem' AND c.name = N'IsDeleted'
)
BEGIN
    ALTER TABLE [dbo].[OrderItem]
    ADD [IsDeleted] bit NOT NULL CONSTRAINT [DF_OrderItem_IsDeleted] DEFAULT 0;

    -- Optional: index for filtered queries if you filter by IsDeleted in SQL
    -- CREATE NONCLUSTERED INDEX [IX_OrderItem_OrderId_IsDeleted] ON [dbo].[OrderItem] ([OrderId], [IsDeleted]) WHERE ([IsDeleted]=(0));
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.name = N'OrderItem' AND c.name = N'UpdatedDate'
)
BEGIN
    ALTER TABLE [dbo].[OrderItem]
    ADD [UpdatedDate] datetime2(0) NULL;
END
GO
