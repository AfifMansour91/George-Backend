-- Migration: MultiSite Phase 2 — per-site product images (ProductSiteImage).
-- A product can have different images per branch. When rows exist for a (product, site), they replace
-- the canonical ProductImage list for that site (effective view). Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteImage]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteImage]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteImage];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteImage]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteImage] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [Url] nvarchar(1000) NOT NULL,
        [SortOrder] int NOT NULL DEFAULT 0,
        CONSTRAINT [PK_ProductSiteImage] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'Url') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [Url] nvarchar(1000) NOT NULL DEFAULT '';
IF COL_LENGTH(N'[dbo].[ProductSiteImage]', N'SortOrder') IS NULL ALTER TABLE [dbo].[ProductSiteImage] ADD [SortOrder] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteImage_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteImage]'))
    CREATE INDEX [IX_ProductSiteImage_Product_Site] ON [dbo].[ProductSiteImage] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteImage_Product')
    ALTER TABLE [dbo].[ProductSiteImage] ADD CONSTRAINT [FK_ProductSiteImage_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteImage_Site')
    ALTER TABLE [dbo].[ProductSiteImage] ADD CONSTRAINT [FK_ProductSiteImage_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO
