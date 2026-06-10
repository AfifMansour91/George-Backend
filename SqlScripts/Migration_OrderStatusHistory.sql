-- Order status transition log for dashboard handling-time KPIs.
-- Run once per environment before deploying API that writes/reads this table.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderStatusHistory')
BEGIN
    CREATE TABLE [dbo].[OrderStatusHistory] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [OrderId] [int] NOT NULL,
        [Status] [nvarchar](20) NOT NULL,
        [OccurredAt] [datetime2](0) NOT NULL,
        CONSTRAINT [PK_OrderStatusHistory] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrderStatusHistory_Order] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Order] ([Id]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_OrderStatusHistory_OrderId_OccurredAt]
        ON [dbo].[OrderStatusHistory] ([OrderId], [OccurredAt]);

    PRINT 'Created OrderStatusHistory table';
END
ELSE
    PRINT 'OrderStatusHistory table already exists';
GO

-- One baseline row per existing order (current status at last update) so dashboards are not empty until new transitions are logged.
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderStatusHistory')
BEGIN
    INSERT INTO [dbo].[OrderStatusHistory] ([OrderId], [Status], [OccurredAt])
    SELECT o.[Id], o.[Status], COALESCE(o.[UpdatedDate], o.[CreationTime])
    FROM [dbo].[Order] o
    WHERE o.[IsDeleted] = 0
      AND NOT EXISTS (
          SELECT 1 FROM [dbo].[OrderStatusHistory] h WHERE h.[OrderId] = o.[Id]
      );
END
GO
