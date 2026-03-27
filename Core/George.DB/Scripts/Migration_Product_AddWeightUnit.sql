-- Migration: Add ShowAsMl and WeightUnit to Product and TemplateProduct
-- Date: 2026-03-27
-- Description: Adds ShowAsMl (bool) and WeightUnit (nvarchar(5)) columns to the Product and TemplateProduct tables
--              to support displaying product weight as ml or grams in addition to kg.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Product]')
    AND name = 'ShowAsMl'
)
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [ShowAsMl] [bit] NULL;

    PRINT 'Added ShowAsMl column to Product table';
END
ELSE
BEGIN
    PRINT 'ShowAsMl column already exists in Product table';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Product]')
    AND name = 'WeightUnit'
)
BEGIN
    ALTER TABLE [dbo].[Product]
    ADD [WeightUnit] [nvarchar](5) NULL;

    PRINT 'Added WeightUnit column to Product table';
END
ELSE
BEGIN
    PRINT 'WeightUnit column already exists in Product table';
END
GO

-- TemplateProduct table

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProduct]')
    AND name = 'ShowAsMl'
)
BEGIN
    ALTER TABLE [dbo].[TemplateProduct]
    ADD [ShowAsMl] [bit] NULL;

    PRINT 'Added ShowAsMl column to TemplateProduct table';
END
ELSE
BEGIN
    PRINT 'ShowAsMl column already exists in TemplateProduct table';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[TemplateProduct]')
    AND name = 'WeightUnit'
)
BEGIN
    ALTER TABLE [dbo].[TemplateProduct]
    ADD [WeightUnit] [nvarchar](5) NULL;

    PRINT 'Added WeightUnit column to TemplateProduct table';
END
ELSE
BEGIN
    PRINT 'WeightUnit column already exists in TemplateProduct table';
END
GO
