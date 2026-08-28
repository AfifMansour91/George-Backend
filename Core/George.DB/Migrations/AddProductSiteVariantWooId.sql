-- Migration: MultiSite Phase 2 - per-site WooCommerce VARIATION id (ProductSiteVariantWooId).
-- A variant of a product shared across sites (network mode) has a DIFFERENT Woo variation id in each store; the
-- single ProductVariant.WooCommerceVariationId column can only track one, so syncing a shared variable product to
-- the 2nd store reused the 1st store's variation ids. The PUT to those ids 404'd, the variation was recreated, and
-- then the orphan-cleanup deleted the recreation (it tracked the OLD id) - leaving the 2nd store with ZERO
-- variations, i.e. a variable product that WooCommerce reports as out of stock. This table maps
-- (ProductVariantId, SiteId) -> WooCommerceVariationId. Consulted only for network-managed accounts. Idempotent.
-- Mirrors ProductSiteWooId (parent) / CategorySiteWooId.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'Id') IS NULL
    DROP TABLE [dbo].[ProductSiteVariantWooId];
GO

IF OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ProductSiteVariantWooId] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [ProductVariantId] int NOT NULL,
        [SiteId] int NOT NULL,
        [ProductId] int NOT NULL,
        [WooCommerceVariationId] int NOT NULL,
        CONSTRAINT [PK_ProductSiteVariantWooId] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'ProductVariantId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [ProductVariantId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'SiteId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'ProductId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [ProductId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[ProductSiteVariantWooId]', N'WooCommerceVariationId') IS NULL ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD [WooCommerceVariationId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_ProductSiteVariantWooId_Variant_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]'))
    CREATE UNIQUE INDEX [UX_ProductSiteVariantWooId_Variant_Site] ON [dbo].[ProductSiteVariantWooId] ([ProductVariantId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductSiteVariantWooId_Product_Site' AND object_id = OBJECT_ID(N'[dbo].[ProductSiteVariantWooId]'))
    CREATE INDEX [IX_ProductSiteVariantWooId_Product_Site] ON [dbo].[ProductSiteVariantWooId] ([ProductId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantWooId_ProductVariant')
    ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD CONSTRAINT [FK_ProductSiteVariantWooId_ProductVariant]
    FOREIGN KEY ([ProductVariantId]) REFERENCES [dbo].[ProductVariant] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProductSiteVariantWooId_Site')
    ALTER TABLE [dbo].[ProductSiteVariantWooId] ADD CONSTRAINT [FK_ProductSiteVariantWooId_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- Seed from the existing single-field id ONLY for variants whose product is on exactly ONE site (where
-- WooCommerceVariationId is unambiguously that site's id). Variants of products shared across multiple sites are
-- intentionally NOT seeded: their per-site variation ids are resolved fresh (match by attribute signature against
-- each store's live variations) on next sync, avoiding seeding a wrong store's id.
INSERT INTO [dbo].[ProductSiteVariantWooId] ([ProductVariantId], [SiteId], [ProductId], [WooCommerceVariationId])
SELECT v.[Id], ps.[SiteId], v.[ProductId], v.[WooCommerceVariationId]
FROM [dbo].[ProductVariant] v
JOIN [dbo].[ProductSite] ps ON ps.[ProductId] = v.[ProductId]
WHERE v.[WooCommerceVariationId] IS NOT NULL
  AND v.[IsDeleted] = 0
  AND (SELECT COUNT(*) FROM [dbo].[ProductSite] ps2 WHERE ps2.[ProductId] = v.[ProductId]) = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[ProductSiteVariantWooId] x WHERE x.[ProductVariantId] = v.[Id] AND x.[SiteId] = ps.[SiteId]);
GO
