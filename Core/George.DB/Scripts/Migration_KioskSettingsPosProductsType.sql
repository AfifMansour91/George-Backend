-- Migration: Add PosProductsType and PosProductsCategoryId to KioskSettings
-- PosProductsType: 'upsells' | 'category' | 'combined'
-- PosProductsCategoryId: optional category for POS products when type is category or combined

USE [George]
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KioskSettings]') AND name = 'PosProductsType')
BEGIN
    ALTER TABLE [dbo].[KioskSettings] ADD [PosProductsType] [nvarchar](20) NULL;
    PRINT 'Added PosProductsType to KioskSettings'
END
ELSE
    PRINT 'PosProductsType already exists on KioskSettings'
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KioskSettings]') AND name = 'PosProductsCategoryId')
BEGIN
    ALTER TABLE [dbo].[KioskSettings] ADD [PosProductsCategoryId] [int] NULL;
    PRINT 'Added PosProductsCategoryId to KioskSettings'
END
ELSE
    PRINT 'PosProductsCategoryId already exists on KioskSettings'
GO

PRINT 'Migration_KioskSettingsPosProductsType completed successfully'
GO
