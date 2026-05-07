-- Migration: Extend Brand table with slug, description, image, SEO, hierarchy, WooCommerce link
-- Date: 2026-05-07
-- Description: Brand was previously a name-only entity. This migration extends it with the fields
--              needed for the new Brands management feature (mirrors Category):
--              Slug, Description, ImageUrl, IconUrl, ParentBrandId (self-FK), SortOrder,
--              IsEnabled, SeoTitle, SeoDescription, WooCommerceBrandId, SourceGlobalBrandId.
--              Also adds an index on WooCommerceBrandId for fast lookups during sync.
--
-- Idempotent: every column / index / FK is wrapped in IF NOT EXISTS guards.

------------------------------------------------------------
-- 1. Add new columns to Brand
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'Slug')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [Slug] [nvarchar](200) NULL;
    PRINT 'Added Slug column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'Description')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [Description] [nvarchar](2000) NULL;
    PRINT 'Added Description column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'ImageUrl')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [ImageUrl] [nvarchar](1000) NULL;
    PRINT 'Added ImageUrl column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'IconUrl')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [IconUrl] [nvarchar](1000) NULL;
    PRINT 'Added IconUrl column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'ParentBrandId')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [ParentBrandId] [int] NULL;
    PRINT 'Added ParentBrandId column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'SortOrder')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [SortOrder] [int] NULL;
    PRINT 'Added SortOrder column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'IsEnabled')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [IsEnabled] [bit] NULL;
    PRINT 'Added IsEnabled column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'SeoTitle')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [SeoTitle] [nvarchar](200) NULL;
    PRINT 'Added SeoTitle column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'SeoDescription')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [SeoDescription] [nvarchar](500) NULL;
    PRINT 'Added SeoDescription column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'WooCommerceBrandId')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [WooCommerceBrandId] [int] NULL;
    PRINT 'Added WooCommerceBrandId column to Brand table';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Brand]') AND name = 'SourceGlobalBrandId')
BEGIN
    ALTER TABLE [dbo].[Brand] ADD [SourceGlobalBrandId] [int] NULL;
    PRINT 'Added SourceGlobalBrandId column to Brand table';
END
GO

------------------------------------------------------------
-- 2. Indexes
------------------------------------------------------------

-- Index on ParentBrandId for hierarchical queries.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Brand_ParentBrandId' AND object_id = OBJECT_ID(N'[dbo].[Brand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Brand_ParentBrandId] ON [dbo].[Brand]([ParentBrandId] ASC) ON [PRIMARY];
    PRINT 'Created index IX_Brand_ParentBrandId';
END
GO

-- Index on WooCommerceBrandId — fast lookup when syncing.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Brand_WooCommerceBrandId' AND object_id = OBJECT_ID(N'[dbo].[Brand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Brand_WooCommerceBrandId] ON [dbo].[Brand]([WooCommerceBrandId] ASC)
        WHERE [WooCommerceBrandId] IS NOT NULL
        ON [PRIMARY];
    PRINT 'Created index IX_Brand_WooCommerceBrandId';
END
GO

-- Index on SourceGlobalBrandId.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Brand_SourceGlobalBrandId' AND object_id = OBJECT_ID(N'[dbo].[Brand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Brand_SourceGlobalBrandId] ON [dbo].[Brand]([SourceGlobalBrandId] ASC)
        WHERE [SourceGlobalBrandId] IS NOT NULL
        ON [PRIMARY];
    PRINT 'Created index IX_Brand_SourceGlobalBrandId';
END
GO

-- Optional: filtered unique index on (AccountId, Slug) so slugs are unique per account
-- (only when Slug is set and Brand isn't soft-deleted). Mirrors Category-style uniqueness.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Brand_AccountId_Slug_NotDeleted' AND object_id = OBJECT_ID(N'[dbo].[Brand]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_Brand_AccountId_Slug_NotDeleted] ON [dbo].[Brand]
    (
        [AccountId] ASC,
        [Slug] ASC
    )
    WHERE ([IsDeleted] = 0 AND [AccountId] IS NOT NULL AND [Slug] IS NOT NULL)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY];
    PRINT 'Created unique index UX_Brand_AccountId_Slug_NotDeleted';
END
GO

------------------------------------------------------------
-- 3. Foreign keys
------------------------------------------------------------

-- Self-FK: ParentBrandId -> Brand.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Brand_Parent' AND parent_object_id = OBJECT_ID(N'[dbo].[Brand]'))
BEGIN
    ALTER TABLE [dbo].[Brand] WITH CHECK ADD CONSTRAINT [FK_Brand_Parent]
        FOREIGN KEY([ParentBrandId]) REFERENCES [dbo].[Brand]([Id]);
    ALTER TABLE [dbo].[Brand] CHECK CONSTRAINT [FK_Brand_Parent];
    PRINT 'Added FK_Brand_Parent';
END
GO

-- SourceGlobalBrandId -> GlobalBrand.Id (only adds the FK if GlobalBrand exists; the GlobalBrand
-- migration runs first if applied in order, but this guard keeps the script safe in any order).
IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GlobalBrand')
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Brand_SourceGlobalBrand' AND parent_object_id = OBJECT_ID(N'[dbo].[Brand]'))
BEGIN
    ALTER TABLE [dbo].[Brand] WITH CHECK ADD CONSTRAINT [FK_Brand_SourceGlobalBrand]
        FOREIGN KEY([SourceGlobalBrandId]) REFERENCES [dbo].[GlobalBrand]([Id]);
    ALTER TABLE [dbo].[Brand] CHECK CONSTRAINT [FK_Brand_SourceGlobalBrand];
    PRINT 'Added FK_Brand_SourceGlobalBrand';
END
ELSE
BEGIN
    PRINT 'Skipped FK_Brand_SourceGlobalBrand (GlobalBrand table not yet created or FK already exists)';
END
GO

PRINT 'Migration_ExtendBrandTable complete.';
