-- Migration: Add UserPreference table for per-user UI preferences (product list view/filters, etc.)
-- Date: 2026-02-19
-- Description: One row per user; PreferencesJson stores JSON with keys like myProducts_viewPrefs, globalCatalog_viewPrefs.

USE [George]
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[UserPreference]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[UserPreference](
        [UserId] [int] NOT NULL,
        [PreferencesJson] [nvarchar](max) NULL,
        CONSTRAINT [PK_UserPreference] PRIMARY KEY CLUSTERED ([UserId] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
    PRINT 'Created UserPreference table'
END
ELSE
BEGIN
    PRINT 'UserPreference table already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_UserPreference_User')
BEGIN
    ALTER TABLE [dbo].[UserPreference] WITH CHECK ADD CONSTRAINT [FK_UserPreference_User] FOREIGN KEY([UserId])
    REFERENCES [dbo].[User] ([Id])
    ON DELETE CASCADE
    ALTER TABLE [dbo].[UserPreference] CHECK CONSTRAINT [FK_UserPreference_User]
    PRINT 'Added FK_UserPreference_User'
END
GO

PRINT 'Migration_AddUserPreference completed successfully'
GO
