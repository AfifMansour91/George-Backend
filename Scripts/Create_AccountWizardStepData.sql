-- Matches George.DB.Models.AccountWizardStepData (Id INT, AccountId INT, SiteId INT NULL, StepNumber INT, DataJson NVARCHAR(MAX), CreationTime, UpdatedDate).
-- StepNumber = 0 stores the full wizard session JSON blob.

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountWizardStepData')
BEGIN
    CREATE TABLE [dbo].[AccountWizardStepData] (
        [Id]           INT IDENTITY(1,1) NOT NULL,
        [AccountId]    INT NOT NULL,
        [SiteId]       INT NULL,
        [StepNumber]   INT NOT NULL,
        [DataJson]     NVARCHAR(MAX) NULL,
        [CreationTime] DATETIME2(2) NOT NULL CONSTRAINT [DF_AccountWizardStepData_CreationTime] DEFAULT (GETUTCDATE()),
        [UpdatedDate]  DATETIME2(2) NULL,
        CONSTRAINT [PK_AccountWizardStepData] PRIMARY KEY CLUSTERED ([Id]),
        CONSTRAINT [UQ_AccountWizardStepData_AccountId_SiteId_StepNumber] UNIQUE ([AccountId], [SiteId], [StepNumber])
    );

    CREATE NONCLUSTERED INDEX [IX_AccountWizardStepData_AccountId_SiteId_StepNumber]
        ON [dbo].[AccountWizardStepData] ([AccountId], [SiteId], [StepNumber]);

    -- Optional FKs if Account and Site tables exist
    -- ALTER TABLE [dbo].[AccountWizardStepData] ADD CONSTRAINT [FK_AccountWizardStepData_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id]);
    -- ALTER TABLE [dbo].[AccountWizardStepData] ADD CONSTRAINT [FK_AccountWizardStepData_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site]([Id]);
END
GO
