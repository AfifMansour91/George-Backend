-- Migration: Add BrandSite junction table (many-to-many Brand <-> Site)
-- Date: 2026-05-07
-- Description: Mirrors CategorySite. Lets a single Brand be available on multiple Sites
--              within the same Account.
--
-- Idempotent.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'BrandSite' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[BrandSite](
        [BrandId] [int] NOT NULL,
        [SiteId]  [int] NOT NULL,
     CONSTRAINT [PK_BrandSite] PRIMARY KEY CLUSTERED
    (
        [BrandId] ASC,
        [SiteId]  ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];

    PRINT 'Created BrandSite table';
END
ELSE
BEGIN
    PRINT 'BrandSite table already exists';
END
GO

-- Index on SiteId for "all brands on this site" queries.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_BrandSite_SiteId' AND object_id = OBJECT_ID(N'[dbo].[BrandSite]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_BrandSite_SiteId] ON [dbo].[BrandSite]([SiteId] ASC) ON [PRIMARY];
    PRINT 'Created index IX_BrandSite_SiteId';
END
GO

-- Foreign keys.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BrandSite_Brand' AND parent_object_id = OBJECT_ID(N'[dbo].[BrandSite]'))
BEGIN
    ALTER TABLE [dbo].[BrandSite] WITH CHECK ADD CONSTRAINT [FK_BrandSite_Brand]
        FOREIGN KEY([BrandId]) REFERENCES [dbo].[Brand]([Id]);
    ALTER TABLE [dbo].[BrandSite] CHECK CONSTRAINT [FK_BrandSite_Brand];
    PRINT 'Added FK_BrandSite_Brand';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BrandSite_Site' AND parent_object_id = OBJECT_ID(N'[dbo].[BrandSite]'))
BEGIN
    ALTER TABLE [dbo].[BrandSite] WITH CHECK ADD CONSTRAINT [FK_BrandSite_Site]
        FOREIGN KEY([SiteId]) REFERENCES [dbo].[Site]([Id]);
    ALTER TABLE [dbo].[BrandSite] CHECK CONSTRAINT [FK_BrandSite_Site];
    PRINT 'Added FK_BrandSite_Site';
END
GO

PRINT 'Migration_AddBrandSiteTable complete.';
