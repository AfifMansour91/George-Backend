-- Migration: Add KioskSettings table (1:1 with Account), remove JSON column from Account
-- Description: Store kiosk design/behavior settings in a proper table instead of Account.KioskSettingsJson

USE [George]
GO

-- Create KioskSettings table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KioskSettings')
BEGIN
    CREATE TABLE [dbo].[KioskSettings] (
        [AccountId] [int] NOT NULL,
        [KioskLogoUrl] [nvarchar](1000) NULL,
        [HeaderBgColor] [nvarchar](50) NULL,
        [HomeBgType] [nvarchar](20) NULL,
        [HomeVideoUrl] [nvarchar](2000) NULL,
        [HomeImageUrls] [nvarchar](4000) NULL,
        [HomeImageIntervalSeconds] [int] NULL,
        [PrimaryColor] [nvarchar](50) NULL,
        [SecondaryColor] [nvarchar](50) NULL,
        [PosProductsTitle] [nvarchar](500) NULL,
        [CreditEnabled] [bit] NOT NULL DEFAULT(0),
        [CashAtRegisterEnabled] [bit] NOT NULL DEFAULT(1),
        CONSTRAINT [PK_KioskSettings] PRIMARY KEY CLUSTERED ([AccountId] ASC),
        CONSTRAINT [FK_KioskSettings_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account] ([Id]) ON DELETE CASCADE
    );
    PRINT 'Created KioskSettings table'
END
ELSE
BEGIN
    PRINT 'KioskSettings table already exists'
END
GO

-- Optionally migrate existing JSON data (if KioskSettingsJson was used) - run once if you had data
-- INSERT INTO [dbo].[KioskSettings] (AccountId, ...) SELECT Id, ... FROM Account WHERE KioskSettingsJson IS NOT NULL AND LEN(KioskSettingsJson) > 0
-- (Omitted for simplicity; add if you need to migrate existing rows.)

-- Drop JSON column from Account
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'KioskSettingsJson')
BEGIN
    ALTER TABLE [dbo].[Account] DROP COLUMN [KioskSettingsJson];
    PRINT 'Dropped KioskSettingsJson column from Account table'
END
ELSE
BEGIN
    PRINT 'KioskSettingsJson column does not exist in Account table'
END
GO

PRINT 'Migration_AddKioskSettingsTable completed successfully'
GO
