-- Migration: Multi-site notification settings.
-- Adds SiteId to AccountNotificationSettings:
--   SiteId NULL  = account-level default row (existing rows stay as-is => zero behavior change).
--   SiteId set   = full per-site override row (whole-row copy semantics; no field-level fallback).
-- Replaces the per-account UNIQUE constraint with a per-(AccountId, SiteId) unique index.
-- SQL Server treats NULL as a value in unique indexes, so exactly one default row per account is still enforced.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.AccountNotificationSettings') AND name = N'SiteId')
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings] ADD [SiteId] [int] NULL;
    PRINT 'Added AccountNotificationSettings.SiteId';
END
ELSE
    PRINT 'AccountNotificationSettings.SiteId already exists';
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AccountNotificationSettings_Site')
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings] WITH CHECK
        ADD CONSTRAINT [FK_AccountNotificationSettings_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site] ([Id]);
    PRINT 'Added FK_AccountNotificationSettings_Site';
END
GO

-- The original table used a UNIQUE constraint; later environments may have it as a unique index. Handle both.
IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'UQ_AccountNotificationSettings_AccountId' AND parent_object_id = OBJECT_ID(N'dbo.AccountNotificationSettings'))
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings] DROP CONSTRAINT [UQ_AccountNotificationSettings_AccountId];
    PRINT 'Dropped UNIQUE constraint UQ_AccountNotificationSettings_AccountId';
END
ELSE IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_AccountNotificationSettings_AccountId' AND object_id = OBJECT_ID(N'dbo.AccountNotificationSettings'))
BEGIN
    DROP INDEX [UQ_AccountNotificationSettings_AccountId] ON [dbo].[AccountNotificationSettings];
    PRINT 'Dropped unique index UQ_AccountNotificationSettings_AccountId';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_AccountNotificationSettings_AccountId_SiteId' AND object_id = OBJECT_ID(N'dbo.AccountNotificationSettings'))
BEGIN
    CREATE UNIQUE INDEX [UQ_AccountNotificationSettings_AccountId_SiteId]
        ON [dbo].[AccountNotificationSettings] ([AccountId], [SiteId]);
    PRINT 'Created UQ_AccountNotificationSettings_AccountId_SiteId';
END
GO
