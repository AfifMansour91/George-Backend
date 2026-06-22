-- Migration: MultiSite Phase 2 — per-site product override layer.
-- Adds: ProductSiteOverride + ProductSiteVariantStock tables, Product.ManagementMode/OwnerSiteId,
--       Account.ManagementMode. Idempotent; run in SSMS or sqlcmd against your George DB.
-- See MultiSite/חוזה-בקאנד-מולטיסייט-Phase2-Override.md.

-- 1. Product: management mode columns
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = N'ManagementMode')
    ALTER TABLE [dbo].[Product] ADD [ManagementMode] nvarchar(20) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Product]') AND name = N'OwnerSiteId')
    ALTER TABLE [dbo].[Product] ADD [OwnerSiteId] int NULL;
GO

-- 2. Account: ongoing management mode
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = N'ManagementMode')
    ALTER TABLE [dbo].[Account] ADD [ManagementMode] nvarchar(20) NULL;
GO

-- 3. ProductSiteOverride table
IF OBJECT_ID(N'[dbo].[ProductSiteOverride]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteOverride] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [CreationTime] datetime2(0) NOT NULL,
        [UpdatedDate] datetime2(0) NULL,
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [AccountId] int NULL,
        [IsExcluded] bit NOT NULL DEFAULT 0,
        [Price] decimal(18, 2) NULL,
        [SalePrice] decimal(18, 2) NULL,
        [SalePriceStartDate] datetime2(0) NULL,
        [SalePriceEndDate] datetime2(0) NULL,
        [Availability] bit NULL,
        [StockManagementTypeId] int NULL,
        [StockStatusId] int NULL,
        [StockQuantity] decimal(18, 4) NULL,
        [VariationStockByQuantity] bit NULL,
        [LowStockThreshold] decimal(18, 4) NULL,
        CONSTRAINT [PK_ProductSiteOverride] PRIMARY KEY ([Id])
    );
END
GO

-- 3a. Self-heal columns (in case the table pre-existed from a partial run without all columns)
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'IsDeleted') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'CreationTime') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [CreationTime] datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'UpdatedDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [UpdatedDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'AccountId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [AccountId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'IsExcluded') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [IsExcluded] bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Price') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Price] decimal(18, 2) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SalePrice') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SalePrice] decimal(18, 2) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SalePriceStartDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SalePriceStartDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'SalePriceEndDate') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [SalePriceEndDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'Availability') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [Availability] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StockManagementTypeId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StockManagementTypeId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StockStatusId') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StockStatusId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'StockQuantity') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [StockQuantity] decimal(18, 4) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'VariationStockByQuantity') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [VariationStockByQuantity] bit NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteOverride]', N'LowStockThreshold') IS NULL ALTER TABLE [dbo].[ProductSiteOverride] ADD [LowStockThreshold] decimal(18, 4) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteOverride_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteOverride]'))
    CREATE UNIQUE INDEX [UX_ProductSiteOverride_Product_Site] ON [dbo].[ProductSiteOverride] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteOverride_SiteId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteOverride]'))
    CREATE INDEX [IX_ProductSiteOverride_SiteId] ON [dbo].[ProductSiteOverride] ([SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteOverride_AccountId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteOverride]'))
    CREATE INDEX [IX_ProductSiteOverride_AccountId] ON [dbo].[ProductSiteOverride] ([AccountId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteOverride_Product')
    ALTER TABLE [dbo].[ProductSiteOverride] ADD CONSTRAINT [FK_ProductSiteOverride_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteOverride_Site')
    ALTER TABLE [dbo].[ProductSiteOverride] ADD CONSTRAINT [FK_ProductSiteOverride_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- 4. ProductSiteVariantStock table
IF OBJECT_ID(N'[dbo].[ProductSiteVariantStock]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteVariantStock] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [IsDeleted] bit NOT NULL DEFAULT 0,
        [CreationTime] datetime2(0) NOT NULL,
        [UpdatedDate] datetime2(0) NULL,
        [ProductVariantId] int NOT NULL,
        [SiteId] int NOT NULL,
        [ProductId] int NULL,
        [StockQuantity] decimal(18, 4) NULL,
        [StockStatusId] int NULL,
        CONSTRAINT [PK_ProductSiteVariantStock] PRIMARY KEY ([Id])
    );
END
GO

-- 4a. Self-heal columns (in case the table pre-existed from a partial run without all columns)
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'IsDeleted') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [IsDeleted] bit NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'CreationTime') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [CreationTime] datetime2(0) NOT NULL DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'UpdatedDate') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [UpdatedDate] datetime2(0) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'ProductVariantId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [ProductVariantId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [ProductId] int NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'StockQuantity') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [StockQuantity] decimal(18, 4) NULL;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'StockStatusId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [StockStatusId] int NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteVariantStock_Variant_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantStock]'))
    CREATE UNIQUE INDEX [UX_ProductSiteVariantStock_Variant_Site] ON [dbo].[ProductSiteVariantStock] ([ProductVariantId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteVariantStock_SiteId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantStock]'))
    CREATE INDEX [IX_ProductSiteVariantStock_SiteId] ON [dbo].[ProductSiteVariantStock] ([SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteVariantStock_ProductId' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantStock]'))
    CREATE INDEX [IX_ProductSiteVariantStock_ProductId] ON [dbo].[ProductSiteVariantStock] ([ProductId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantStock_ProductVariant')
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD CONSTRAINT [FK_ProductSiteVariantStock_ProductVariant]
    FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariant] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantStock_Site')
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD CONSTRAINT [FK_ProductSiteVariantStock_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO
