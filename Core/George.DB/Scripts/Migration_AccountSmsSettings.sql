-- Per-account SMS provider credentials (חשבון SMS פר-חשבון).
-- No row / IsEnabled=0 / empty token => the account sends through the system-wide SMS account.
-- Currently only ActiveTrail is supported; the Provider column exists for future providers.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountSmsSettings')
BEGIN
    CREATE TABLE [dbo].[AccountSmsSettings] (
        [Id]             INT IDENTITY (1, 1) NOT NULL,
        [AccountId]      INT                 NOT NULL,
        [CreationTime]   DATETIME2 (0)       NOT NULL CONSTRAINT [DF_AccountSmsSettings_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [UpdatedDate]    DATETIME2 (0)       NULL,
        [CreationUserId] INT                 NULL,
        [UpdateUserId]   INT                 NULL,
        [IsEnabled]      BIT                 NOT NULL CONSTRAINT [DF_AccountSmsSettings_IsEnabled] DEFAULT ((0)),
        [Provider]       NVARCHAR (20)       NOT NULL CONSTRAINT [DF_AccountSmsSettings_Provider] DEFAULT (N'ActiveTrail'),
        [ApiBaseUrl]     NVARCHAR (500)      NULL,
        [ApiToken]       NVARCHAR (500)      NULL,
        [FromName]       NVARCHAR (100)      NULL,
        [SourcePhone]    NVARCHAR (50)       NULL,
        CONSTRAINT [PK_AccountSmsSettings] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccountSmsSettings_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account] ([Id])
    );

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AccountSmsSettings_AccountId]
        ON [dbo].[AccountSmsSettings] ([AccountId] ASC);

    PRINT 'Created AccountSmsSettings'
END
ELSE
    PRINT 'AccountSmsSettings already exists'
GO
