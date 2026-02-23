-- Migration: Add Order and OrderItem tables (Sprint 2).
-- Order: per-site orders from website/kiosk/phone. OrderItem: line items with product snapshot.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Order')
BEGIN
    CREATE TABLE [dbo].[Order] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [IsDeleted] [bit] NOT NULL CONSTRAINT [DF_Order_IsDeleted] DEFAULT (0),
        [CreationTime] [datetime2](0) NOT NULL CONSTRAINT [DF_Order_CreationTime] DEFAULT (sysutcdatetime()),
        [UpdatedDate] [datetime2](0) NULL,
        [CreationUserId] [int] NULL,
        [UpdateUserId] [int] NULL,
        [AccountId] [int] NOT NULL,
        [SiteId] [int] NOT NULL,
        [OrderNumber] [nvarchar](50) NOT NULL,
        [Source] [nvarchar](20) NOT NULL,
        [Status] [nvarchar](20) NOT NULL CONSTRAINT [DF_Order_Status] DEFAULT ('New'),
        [DeliveryType] [nvarchar](20) NULL,
        [PaymentStatus] [nvarchar](20) NOT NULL CONSTRAINT [DF_Order_PaymentStatus] DEFAULT ('Unpaid'),
        [CustomerName] [nvarchar](200) NULL,
        [CustomerPhone] [nvarchar](50) NULL,
        [CustomerId] [int] NULL,
        [DeliveryAddress] [nvarchar](500) NULL,
        [DeliveryDate] [datetime2](0) NULL,
        [DeliveryTime] [nvarchar](20) NULL,
        [PickupDate] [datetime2](0) NULL,
        [PickupTime] [nvarchar](20) NULL,
        [ManagerNote] [nvarchar](2000) NULL,
        [CustomerNote] [nvarchar](2000) NULL,
        [DeliveryNote] [nvarchar](500) NULL,
        [SubTotal] [decimal](18,2) NULL,
        [ShippingCost] [decimal](18,2) NULL,
        [Total] [decimal](18,2) NULL,
        [ExternalOrderId] [nvarchar](100) NULL,
        CONSTRAINT [PK_Order] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_Order_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Order_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]) ON DELETE NO ACTION
    );
    CREATE NONCLUSTERED INDEX [IX_Order_SiteId_IsDeleted] ON [dbo].[Order] ([SiteId], [IsDeleted]) WHERE ([IsDeleted]=(0));
    CREATE NONCLUSTERED INDEX [IX_Order_OrderNumber] ON [dbo].[Order] ([OrderNumber]) WHERE ([IsDeleted]=(0));
    PRINT 'Created Order table'
END
ELSE
    PRINT 'Order table already exists'
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'OrderItem')
BEGIN
    CREATE TABLE [dbo].[OrderItem] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [OrderId] [int] NOT NULL,
        [ProductId] [int] NULL,
        [ProductVariantId] [int] NULL,
        [Title] [nvarchar](500) NULL,
        [VariantTitle] [nvarchar](200) NULL,
        [Quantity] [decimal](18,4) NOT NULL,
        [UnitWeightGrams] [decimal](18,4) NULL,
        [PricePerUnit] [decimal](18,4) NULL,
        [TotalPrice] [decimal](18,2) NULL,
        [Notes] [nvarchar](500) NULL,
        [SortOrder] [int] NOT NULL CONSTRAINT [DF_OrderItem_SortOrder] DEFAULT (0),
        CONSTRAINT [PK_OrderItem] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrderItem_Order] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Order] ([Id]) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX [IX_OrderItem_OrderId] ON [dbo].[OrderItem] ([OrderId]);
    PRINT 'Created OrderItem table'
END
ELSE
    PRINT 'OrderItem table already exists'
GO
