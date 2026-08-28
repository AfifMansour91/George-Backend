-- Migration: Add TemplateProductBrand join table (many-to-many TemplateProduct <-> GlobalBrand)
-- Date: 2026-05-07
-- Description: Mirrors TemplateProductCategory, which joins TemplateProduct <-> GlobalCategory
--              (template-level entities link to global, not account-level, taxonomies).
--
-- NOTE: Existing TemplateProduct.BrandId points at Brand (account-level), which is a pre-existing
--       inconsistency in the schema. We do NOT backfill from it here - the type mismatch is
--       deliberate. New code should write to TemplateProductBrand against GlobalBrand IDs;
--       the legacy TemplateProduct.BrandId column will be retired in a follow-up migration.
--
-- Idempotent.

------------------------------------------------------------
-- 1. Table
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TemplateProductBrand' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[TemplateProductBrand](
        [TemplateProductId] [int] NOT NULL,
        [GlobalBrandId]     [int] NOT NULL,
        [IsPrimary]         [bit] NOT NULL,
     CONSTRAINT [PK_TemplateProductBrand] PRIMARY KEY CLUSTERED
    (
        [TemplateProductId] ASC,
        [GlobalBrandId]     ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];

    ALTER TABLE [dbo].[TemplateProductBrand] ADD CONSTRAINT [DF_TemplateProductBrand_IsPrimary] DEFAULT (0) FOR [IsPrimary];

    PRINT 'Created TemplateProductBrand table';
END
ELSE
BEGIN
    PRINT 'TemplateProductBrand table already exists';
END
GO

------------------------------------------------------------
-- 2. Index on GlobalBrandId
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_TemplateProductBrand_GlobalBrandId' AND object_id = OBJECT_ID(N'[dbo].[TemplateProductBrand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TemplateProductBrand_GlobalBrandId] ON [dbo].[TemplateProductBrand]([GlobalBrandId] ASC) ON [PRIMARY];
    PRINT 'Created index IX_TemplateProductBrand_GlobalBrandId';
END
GO

------------------------------------------------------------
-- 3. Foreign keys
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TemplateProductBrand_GlobalBrand' AND parent_object_id = OBJECT_ID(N'[dbo].[TemplateProductBrand]'))
BEGIN
    ALTER TABLE [dbo].[TemplateProductBrand] WITH CHECK ADD CONSTRAINT [FK_TemplateProductBrand_GlobalBrand]
        FOREIGN KEY([GlobalBrandId]) REFERENCES [dbo].[GlobalBrand]([Id]);
    ALTER TABLE [dbo].[TemplateProductBrand] CHECK CONSTRAINT [FK_TemplateProductBrand_GlobalBrand];
    PRINT 'Added FK_TemplateProductBrand_GlobalBrand';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_TemplateProductBrand_TemplateProduct' AND parent_object_id = OBJECT_ID(N'[dbo].[TemplateProductBrand]'))
BEGIN
    ALTER TABLE [dbo].[TemplateProductBrand] WITH CHECK ADD CONSTRAINT [FK_TemplateProductBrand_TemplateProduct]
        FOREIGN KEY([TemplateProductId]) REFERENCES [dbo].[TemplateProduct]([Id]);
    ALTER TABLE [dbo].[TemplateProductBrand] CHECK CONSTRAINT [FK_TemplateProductBrand_TemplateProduct];
    PRINT 'Added FK_TemplateProductBrand_TemplateProduct';
END
GO

PRINT 'Migration_AddTemplateProductBrandTable complete.';
