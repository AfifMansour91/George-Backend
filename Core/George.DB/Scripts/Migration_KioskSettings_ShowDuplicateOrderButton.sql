-- Migration: Add ShowDuplicateOrderButton to KioskSettings
-- Description: Toggle to show or hide the "הזמנה חוזרת" (Repeat Order) button in the kiosk. Default 0 (hide).

USE [George]
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KioskSettings]') AND name = 'ShowDuplicateOrderButton')
BEGIN
    ALTER TABLE [dbo].[KioskSettings] ADD [ShowDuplicateOrderButton] [bit] NOT NULL DEFAULT(0);
    PRINT 'Added ShowDuplicateOrderButton to KioskSettings'
END
ELSE
    PRINT 'ShowDuplicateOrderButton already exists on KioskSettings'
GO

PRINT 'Migration_KioskSettings_ShowDuplicateOrderButton completed successfully'
GO
