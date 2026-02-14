-- Migration: Add KioskEnabled column to Account table
-- Date: 2026-02-13
-- Description: Enables kiosk mode per account. When enabled, account settings show "הגדרות קיוסק"
--              and category create/edit shows image + icon for kiosk display.

USE [George]
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'KioskEnabled')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [KioskEnabled] [bit] NOT NULL DEFAULT(0)
    PRINT 'Added KioskEnabled column to Account table'
END
ELSE
BEGIN
    PRINT 'KioskEnabled column already exists in Account table'
END
GO

PRINT 'Migration_AddAccountKioskEnabled completed successfully'
GO
