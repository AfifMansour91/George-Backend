-- Add audit columns to AccountNotificationSettings if they are missing (e.g. table created before audit columns were added).
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountNotificationSettings')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'IsDeleted')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [IsDeleted] [bit] NOT NULL CONSTRAINT [DF_AccountNotificationSettings_IsDeleted] DEFAULT (0);
        PRINT 'Added IsDeleted'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'CreationTime')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [CreationTime] [datetime2](0) NOT NULL CONSTRAINT [DF_AccountNotificationSettings_CreationTime] DEFAULT (sysutcdatetime());
        PRINT 'Added CreationTime'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'UpdatedDate')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [UpdatedDate] [datetime2](0) NULL;
        PRINT 'Added UpdatedDate'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'CreationUserId')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [CreationUserId] [int] NULL;
        PRINT 'Added CreationUserId'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'UpdateUserId')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [UpdateUserId] [int] NULL;
        PRINT 'Added UpdateUserId'
    END
END
GO
