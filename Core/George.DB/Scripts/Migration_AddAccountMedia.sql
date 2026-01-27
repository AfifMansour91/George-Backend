-- Migration: Add AccountMedia table for explicit "account uses media" tracking
-- Date: 2026-01-26
-- Description: Enables multiple accounts to use the same media file (e.g. global media).
--              AccountMedia records (AccountId, MediaId) when an account uses a media item.

USE [George]
GO

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[AccountMedia]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[AccountMedia](
        [AccountId] [int] NOT NULL,
        [MediaId] [int] NOT NULL,
        [CreationTime] [datetime2](0) NOT NULL CONSTRAINT [DF_AccountMedia_CreationTime] DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_AccountMedia] PRIMARY KEY CLUSTERED ([AccountId] ASC, [MediaId] ASC)
            WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    ) ON [PRIMARY]
    PRINT 'Created AccountMedia table'
END
ELSE
BEGIN
    PRINT 'AccountMedia table already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AccountMedia_AccountId' AND object_id = OBJECT_ID(N'[dbo].[AccountMedia]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccountMedia_AccountId] ON [dbo].[AccountMedia]([AccountId] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    PRINT 'Created IX_AccountMedia_AccountId'
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AccountMedia_MediaId' AND object_id = OBJECT_ID(N'[dbo].[AccountMedia]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccountMedia_MediaId] ON [dbo].[AccountMedia]([MediaId] ASC)
        WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
    PRINT 'Created IX_AccountMedia_MediaId'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_AccountMedia_Account')
BEGIN
    ALTER TABLE [dbo].[AccountMedia] WITH CHECK ADD CONSTRAINT [FK_AccountMedia_Account] FOREIGN KEY([AccountId])
    REFERENCES [dbo].[Account] ([Id])
    ON DELETE CASCADE
    ALTER TABLE [dbo].[AccountMedia] CHECK CONSTRAINT [FK_AccountMedia_Account]
    PRINT 'Added FK_AccountMedia_Account'
END
GO

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_AccountMedia_Media')
BEGIN
    ALTER TABLE [dbo].[AccountMedia] WITH CHECK ADD CONSTRAINT [FK_AccountMedia_Media] FOREIGN KEY([MediaId])
    REFERENCES [dbo].[Media] ([Id])
    ON DELETE CASCADE
    ALTER TABLE [dbo].[AccountMedia] CHECK CONSTRAINT [FK_AccountMedia_Media]
    PRINT 'Added FK_AccountMedia_Media'
END
GO

PRINT 'Migration_AddAccountMedia completed successfully'
GO
