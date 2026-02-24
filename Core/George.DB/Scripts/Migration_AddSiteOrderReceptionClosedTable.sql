-- Migration: Add SiteOrderReceptionClosed table (Sprint 2 – פתיחה/סגירת קבלת הזמנות).
-- Stores dates when order reception is closed per site for Delivery and/or Pickup.
-- Used by the Open/Close reception modal; can be synced to WooCommerce "אל תכלול תאריכים".

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SiteOrderReceptionClosed')
BEGIN
    CREATE TABLE [dbo].[SiteOrderReceptionClosed] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [SiteId] [int] NOT NULL,
        [ClosedDate] [date] NOT NULL,
        [Type] [nvarchar](20) NOT NULL,
        CONSTRAINT [PK_SiteOrderReceptionClosed] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_SiteOrderReceptionClosed_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [CK_SiteOrderReceptionClosed_Type] CHECK ([Type] IN (N'Delivery', N'Pickup'))
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UX_SiteOrderReceptionClosed_SiteId_ClosedDate_Type]
        ON [dbo].[SiteOrderReceptionClosed] ([SiteId], [ClosedDate], [Type]);
    CREATE NONCLUSTERED INDEX [IX_SiteOrderReceptionClosed_SiteId] ON [dbo].[SiteOrderReceptionClosed] ([SiteId]);
    PRINT 'Created SiteOrderReceptionClosed table';
END
ELSE
    PRINT 'SiteOrderReceptionClosed table already exists';
