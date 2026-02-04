-- Migration: Add ImageUrl and IconUrl to GlobalCategory
-- Date: 2026-02-05
-- Description: Adds ImageUrl and IconUrl columns for category display (image and icon uploads).

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[GlobalCategory]')
    AND name = 'ImageUrl'
)
BEGIN
    ALTER TABLE [dbo].[GlobalCategory]
    ADD [ImageUrl] [nvarchar](1000) NULL;

    PRINT 'Added ImageUrl column to GlobalCategory table';
END
ELSE
BEGIN
    PRINT 'ImageUrl column already exists in GlobalCategory table';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[GlobalCategory]')
    AND name = 'IconUrl'
)
BEGIN
    ALTER TABLE [dbo].[GlobalCategory]
    ADD [IconUrl] [nvarchar](1000) NULL;

    PRINT 'Added IconUrl column to GlobalCategory table';
END
ELSE
BEGIN
    PRINT 'IconUrl column already exists in GlobalCategory table';
END
GO
