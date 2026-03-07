-- Migration: Add Customer table (per-site CRM).
-- Customer has AccountId + SiteId; unique on (SiteId, NormalizedPhone).
-- Run against your database (e.g. in SSMS or sqlcmd).
-- If your tables use different names (e.g. [Orders] instead of [Order]), update the script accordingly.

-- 1. Create Customer table
IF OBJECT_ID(N'[dbo].[Customer]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Customer] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [AccountId] int NOT NULL,
        [SiteId] int NOT NULL,
        [NormalizedPhone] nvarchar(50) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Email] nvarchar(200) NULL,
        [Phone] nvarchar(50) NULL,
        [City] nvarchar(200) NULL,
        [DefaultAddress] nvarchar(500) NULL,
        [Notes] nvarchar(2000) NULL,
        [MarketingApproval] bit NOT NULL DEFAULT 0,
        [MarketingEmail] bit NOT NULL DEFAULT 0,
        [MarketingSms] bit NOT NULL DEFAULT 0,
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [CreationTime] datetime2(0) NOT NULL,
        [UpdatedDate] datetime2(0) NULL,
        CONSTRAINT [PK_Customer] PRIMARY KEY ([Id])
    );
END
GO

-- 2. Unique index: one customer per (SiteId, NormalizedPhone)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customer_SiteId_NormalizedPhone' AND object_id = OBJECT_ID(N'[dbo].[Customer]'))
    CREATE UNIQUE INDEX [IX_Customer_SiteId_NormalizedPhone] ON [dbo].[Customer] ([SiteId], [NormalizedPhone]);
GO

-- 3. FK Customer -> Account (adjust [Account] if your table name differs)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Customer_Account_AccountId')
    ALTER TABLE [dbo].[Customer] ADD CONSTRAINT [FK_Customer_Account_AccountId] 
    FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account] ([Id]);
GO

-- 4. FK Customer -> Site (adjust [Site] if your table name differs)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Customer_Site_SiteId')
    ALTER TABLE [dbo].[Customer] ADD CONSTRAINT [FK_Customer_Site_SiteId] 
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- 5. Ensure Order has CustomerId column if it was added manually (skip if already present)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = N'CustomerId')
BEGIN
    ALTER TABLE [dbo].[Order] ADD [CustomerId] int NULL;
END
GO

-- 6. FK Order -> Customer (only if constraint does not exist)
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Order_Customer_CustomerId')
    ALTER TABLE [dbo].[Order] ADD CONSTRAINT [FK_Order_Customer_CustomerId] 
    FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customer] ([Id]);
GO
