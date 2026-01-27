-- Migration: Remove AccountId column from Media table
-- Date: 2026-01-26
-- Description: Media ownership/usage is tracked only via AccountMedia.
--              Run Migration_AddAccountMedia.sql first if not already applied.

USE [George]
GO

-- Preserve existing ownership: ensure AccountMedia rows exist for media with AccountId
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Media]') AND name = N'AccountId')
   AND EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AccountMedia]') AND type in (N'U'))
BEGIN
    INSERT INTO [dbo].[AccountMedia] ([AccountId], [MediaId], [CreationTime])
    SELECT m.[AccountId], m.[Id], ISNULL(m.[UpdatedDate], m.[CreationTime])
    FROM [dbo].[Media] m
    WHERE m.[AccountId] IS NOT NULL
      AND m.[IsDeleted] = 0
      AND NOT EXISTS (SELECT 1 FROM [dbo].[AccountMedia] am WHERE am.[AccountId] = m.[AccountId] AND am.[MediaId] = m.[Id])
    PRINT 'Backfilled AccountMedia from Media.AccountId'
END
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_Media_Account')
BEGIN
    ALTER TABLE [dbo].[Media] DROP CONSTRAINT [FK_Media_Account]
    PRINT 'Dropped FK_Media_Account'
END
ELSE
BEGIN
    PRINT 'FK_Media_Account does not exist'
END
GO

IF EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_Media_AccountId' AND object_id = OBJECT_ID(N'[dbo].[Media]'))
BEGIN
    DROP INDEX [IX_Media_AccountId] ON [dbo].[Media]
    PRINT 'Dropped IX_Media_AccountId'
END
ELSE
BEGIN
    PRINT 'IX_Media_AccountId does not exist'
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Media]') AND name = N'AccountId')
BEGIN
    ALTER TABLE [dbo].[Media] DROP COLUMN [AccountId]
    PRINT 'Dropped AccountId column from Media'
END
ELSE
BEGIN
    PRINT 'Media.AccountId column does not exist'
END
GO

PRINT 'Migration_RemoveMediaAccountId completed successfully'
GO
