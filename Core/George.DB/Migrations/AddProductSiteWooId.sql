-- Migration: MultiSite Phase 2 — per-site WooCommerce product id (ProductSiteWooId).
-- The same product has a DIFFERENT Woo product id in each site's store; the single Product.WooCommerceId
-- column can only track one, so syncing a multi-site product to the 2nd store used the wrong id and only
-- one site actually updated. This table maps (ProductId, SiteId) -> WooCommerceProductId. Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteWooId]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteWooId];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteWooId]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteWooId] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductId] int NOT NULL,
        [SiteId] int NOT NULL,
        [WooCommerceProductId] int NOT NULL,
        CONSTRAINT [PK_ProductSiteWooId] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteWooId] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteWooId] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteWooId]', N'WooCommerceProductId') IS NULL ALTER TABLE [dbo].[ProductSiteWooId] ADD [WooCommerceProductId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteWooId_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteWooId]'))
    CREATE UNIQUE INDEX [UX_ProductSiteWooId_Product_Site] ON [dbo].[ProductSiteWooId] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteWooId_Product')
    ALTER TABLE [dbo].[ProductSiteWooId] ADD CONSTRAINT [FK_ProductSiteWooId_Product]
    FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteWooId_Site')
    ALTER TABLE [dbo].[ProductSiteWooId] ADD CONSTRAINT [FK_ProductSiteWooId_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- Seed from the existing single-field id ONLY for products on exactly ONE site (where Product.WooCommerceId
-- is unambiguously that site's id). Multi-site products are intentionally NOT seeded: their per-site ids are
-- resolved fresh via the sync's per-site SKU lookup, avoiding seeding a wrong id for the other store(s).
INSERT INTO [dbo].[ProductSiteWooId] ([ProductId], [SiteId], [WooCommerceProductId])
SELECT p.[Id], ps.[SiteId], p.[WooCommerceId]
FROM [dbo].[Product] p
JOIN [dbo].[ProductSite] ps ON ps.[ProductId] = p.[Id]
WHERE p.[WooCommerceId] IS NOT NULL
  AND (SELECT COUNT(*) FROM [dbo].[ProductSite] ps2 WHERE ps2.[ProductId] = p.[Id]) = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[ProductSiteWooId] x WHERE x.[ProductId] = p.[Id] AND x.[SiteId] = ps.[SiteId]);
GO
