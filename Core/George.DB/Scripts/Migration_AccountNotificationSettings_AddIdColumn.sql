-- Run only if AccountNotificationSettings already exists with AccountId as PK and you want to add Id IDENTITY as PK.
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountNotificationSettings')
   AND NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'Id')
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings]
    ADD [Id] [int] IDENTITY(1,1) NOT NULL;

    ALTER TABLE [dbo].[AccountNotificationSettings]
    DROP CONSTRAINT [PK_AccountNotificationSettings];

    ALTER TABLE [dbo].[AccountNotificationSettings]
    ADD CONSTRAINT [PK_AccountNotificationSettings] PRIMARY KEY CLUSTERED ([Id] ASC);

    ALTER TABLE [dbo].[AccountNotificationSettings]
    ADD CONSTRAINT [UQ_AccountNotificationSettings_AccountId] UNIQUE ([AccountId]);

    PRINT 'Added Id column and set as PK; AccountId is unique.'
END
GO
