-- Migration: MultiSite Phase 2 — per-site WooCommerce category id (CategorySiteWooId).
-- A category shared across sites (network mode) has a DIFFERENT Woo category id in each store; the single
-- Category.WooCommerceId column can only track one, so syncing a shared category to the 2nd store reused the
-- 1st store's id and overwrote/corrupted whatever category sat at that id ("categories mess"). This table maps
-- (CategoryId, SiteId) -> WooCommerceCategoryId. Consulted only for network-managed accounts. Idempotent.

-- Self-repair: drop a malformed leftover table missing its [Id] PK (from an earlier partial run) so it is recreated correctly.
IF OBJECT_ID(N'[dbo].[CategorySiteWooId]', N'U') IS NOT NULL AND COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'Id') IS NULL
    DROP TABLE [dbo].[CategorySiteWooId];
GO

IF OBJECT_ID(N'[dbo].[CategorySiteWooId]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[CategorySiteWooId] (
        [Id] int NOT NULL IDENTITY(1, 1),
        [CategoryId] int NOT NULL,
        [SiteId] int NOT NULL,
        [WooCommerceCategoryId] int NOT NULL,
        CONSTRAINT [PK_CategorySiteWooId] PRIMARY KEY ([Id])
    );
END
GO

IF COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'CategoryId') IS NULL ALTER TABLE [dbo].[CategorySiteWooId] ADD [CategoryId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'SiteId') IS NULL ALTER TABLE [dbo].[CategorySiteWooId] ADD [SiteId] int NOT NULL DEFAULT 0;
IF COL_LENGTH(N'[dbo].[CategorySiteWooId]', N'WooCommerceCategoryId') IS NULL ALTER TABLE [dbo].[CategorySiteWooId] ADD [WooCommerceCategoryId] int NOT NULL DEFAULT 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CategorySiteWooId_Category_Site' AND object_id = OBJECT_ID(N'[dbo].[CategorySiteWooId]'))
    CREATE UNIQUE INDEX [UX_CategorySiteWooId_Category_Site] ON [dbo].[CategorySiteWooId] ([CategoryId], [SiteId]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CategorySiteWooId_Category')
    ALTER TABLE [dbo].[CategorySiteWooId] ADD CONSTRAINT [FK_CategorySiteWooId_Category]
    FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([Id]);
GO
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CategorySiteWooId_Site')
    ALTER TABLE [dbo].[CategorySiteWooId] ADD CONSTRAINT [FK_CategorySiteWooId_Site]
    FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
GO

-- Seed from the existing single-field id ONLY for categories on exactly ONE site (where Category.WooCommerceId
-- is unambiguously that site's id). Categories shared across multiple sites are intentionally NOT seeded: their
-- per-site ids are resolved fresh (find-or-create by name) on next sync, avoiding seeding a wrong store's id.
INSERT INTO [dbo].[CategorySiteWooId] ([CategoryId], [SiteId], [WooCommerceCategoryId])
SELECT c.[Id], cs.[SiteId], c.[WooCommerceId]
FROM [dbo].[Category] c
JOIN [dbo].[CategorySite] cs ON cs.[CategoryId] = c.[Id]
WHERE c.[WooCommerceId] IS NOT NULL
  AND (SELECT COUNT(*) FROM [dbo].[CategorySite] cs2 WHERE cs2.[CategoryId] = c.[Id]) = 1
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CategorySiteWooId] x WHERE x.[CategoryId] = c.[Id] AND x.[SiteId] = cs.[SiteId]);
GO
