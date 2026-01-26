-- Migration: Add IsKosherShop and AllowWeighted columns to Account table
-- Date: 2026-01-25
-- Description: Adds IsKosherShop and AllowWeighted boolean fields to Account table
--              to support kosher shop settings and weighted products configuration at account level

USE [George]
GO

-- Check if columns already exist before adding them
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'IsKosherShop')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [IsKosherShop] [bit] NOT NULL DEFAULT(0)
    PRINT 'Added IsKosherShop column to Account table'
END
ELSE
BEGIN
    PRINT 'IsKosherShop column already exists in Account table'
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'AllowWeighted')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [AllowWeighted] [bit] NOT NULL DEFAULT(0)
    PRINT 'Added AllowWeighted column to Account table'
END
ELSE
BEGIN
    PRINT 'AllowWeighted column already exists in Account table'
END
GO

PRINT 'Migration completed successfully'
GO
