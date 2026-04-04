IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = N'CompletionInventoryApplied'
)
BEGIN
    ALTER TABLE [dbo].[Order] ADD [CompletionInventoryApplied] BIT NOT NULL CONSTRAINT [DF_Order_CompletionInventoryApplied] DEFAULT (0);
END
GO
