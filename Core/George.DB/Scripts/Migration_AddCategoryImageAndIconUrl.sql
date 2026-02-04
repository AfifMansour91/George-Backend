-- Migration: Add ImageUrl and IconUrl to Category
-- Date: 2026-02-03
-- Description: Adds ImageUrl and IconUrl columns for kiosk category display (image, icon, or text fallback).

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Category]')
    AND name = 'ImageUrl'
)
BEGIN
    ALTER TABLE [dbo].[Category]
    ADD [ImageUrl] [nvarchar](1000) NULL;

    PRINT 'Added ImageUrl column to Category table';
END
ELSE
BEGIN
    PRINT 'ImageUrl column already exists in Category table';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Category]')
    AND name = 'IconUrl'
)
BEGIN
    ALTER TABLE [dbo].[Category]
    ADD [IconUrl] [nvarchar](1000) NULL;

    PRINT 'Added IconUrl column to Category table';
END
ELSE
BEGIN
    PRINT 'IconUrl column already exists in Category table';
END
GO
