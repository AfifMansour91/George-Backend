-- Migration: KioskSettings use Media table FKs for video and home images
-- Run after Migration_AddKioskSettingsTable. Adds HomeVideoMediaId, creates KioskSettingsHomeImage, removes URL columns.

USE [George]
GO

-- Add HomeVideoMediaId to KioskSettings (FK to Media)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KioskSettings]') AND name = 'HomeVideoMediaId')
BEGIN
    ALTER TABLE [dbo].[KioskSettings] ADD [HomeVideoMediaId] [int] NULL;
    PRINT 'Added HomeVideoMediaId to KioskSettings'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_KioskSettings_HomeVideoMedia')
BEGIN
    ALTER TABLE [dbo].[KioskSettings]
    ADD CONSTRAINT [FK_KioskSettings_HomeVideoMedia] FOREIGN KEY ([HomeVideoMediaId]) REFERENCES [dbo].[Media] ([Id]) ON DELETE SET NULL;
    PRINT 'Added FK_KioskSettings_HomeVideoMedia'
END
GO

-- Create KioskSettingsHomeImage table (AccountId, MediaId, SortOrder)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'KioskSettingsHomeImage')
BEGIN
    CREATE TABLE [dbo].[KioskSettingsHomeImage] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [AccountId] [int] NOT NULL,
        [MediaId] [int] NOT NULL,
        [SortOrder] [int] NOT NULL DEFAULT(0),
        CONSTRAINT [PK_KioskSettingsHomeImage] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_KioskSettingsHomeImage_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_KioskSettingsHomeImage_Media] FOREIGN KEY ([MediaId]) REFERENCES [dbo].[Media] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_KioskSettingsHomeImage_AccountId] ON [dbo].[KioskSettingsHomeImage] ([AccountId]);
    CREATE INDEX [IX_KioskSettingsHomeImage_MediaId] ON [dbo].[KioskSettingsHomeImage] ([MediaId]);
    PRINT 'Created KioskSettingsHomeImage table'
END
GO

-- Drop old URL columns from KioskSettings
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KioskSettings]') AND name = 'HomeVideoUrl')
BEGIN
    ALTER TABLE [dbo].[KioskSettings] DROP COLUMN [HomeVideoUrl];
    PRINT 'Dropped HomeVideoUrl from KioskSettings'
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[KioskSettings]') AND name = 'HomeImageUrls')
BEGIN
    ALTER TABLE [dbo].[KioskSettings] DROP COLUMN [HomeImageUrls];
    PRINT 'Dropped HomeImageUrls from KioskSettings'
END
GO

PRINT 'Migration_KioskSettingsMediaFks completed successfully'
GO
