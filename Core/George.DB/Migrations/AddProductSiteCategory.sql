-- Migration: MultiSite Phase 2 — per-site category assignment (ProductSiteCategory).
-- A product can be assigned to different categories per branch. When rows exist for a (product, site),
-- they replace the canonical ProductCategory for that site (effective view). Idempotent.

IF OBJECT_ID(N'[dbo].[ProductSiteCategory]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteCategory] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [CategoryId] int NOT NULL,
        CONSTRAINT [PK_ProductSiteCategory] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteCategory] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteCategory] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteCategory]', N'CategoryId') IS NULL ALTER TABLE [dbo].[ProductSiteCategory] ADD [CategoryId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteCategory_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteCategory]'))
    CREATE INDEX [IX_ProductSiteCategory_Product_Site] ON [dbo].[ProductSiteCategory] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteCategory_Product_Site_Category' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteCategory]'))
    CREATE UNIQUE INDEX [UX_ProductSiteCategory_Product_Site_Category] ON [dbo].[ProductSiteCategory] ([ProductId], [SiteId], [CategoryId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteCategory_Product')
    ALTER TABLE [dbo].[ProductSiteCategory] ADD CONSTRAINT [FK_ProductSiteCategory_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteCategory_Site')
    ALTER TABLE [dbo].[ProductSiteCategory] ADD CONSTRAINT [FK_ProductSiteCategory_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteCategory_Category')
    ALTER TABLE [dbo].[ProductSiteCategory] ADD CONSTRAINT [FK_ProductSiteCategory_Category]
    FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([Id]);
GO
