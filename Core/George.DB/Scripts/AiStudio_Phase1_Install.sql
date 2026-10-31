-- AI Product Studio, phase 1 (AI_PRODUCT_STUDIO_SPEC.md §8, §11).
-- Adds the job table, the Account feature gate and the Media origin columns.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

-- Account: feature gate + mode + daily cap (pattern: MarketingEnabled).
IF COL_LENGTH(N'dbo.Account', N'AiStudioEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account] ADD [AiStudioEnabled] BIT NOT NULL CONSTRAINT [DF_Account_AiStudioEnabled] DEFAULT (0);
END
GO
IF COL_LENGTH(N'dbo.Account', N'AiStudioMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account] ADD [AiStudioMode] NVARCHAR(20) NULL;
END
GO
IF COL_LENGTH(N'dbo.Account', N'AiStudioDailyCap') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account] ADD [AiStudioDailyCap] INT NULL;
END
GO

-- Media: where a file came from, which job made it, whether it was donated to the community pool.
IF COL_LENGTH(N'dbo.Media', N'Origin') IS NULL
BEGIN
    ALTER TABLE [dbo].[Media] ADD [Origin] NVARCHAR(20) NULL;
END
GO
IF COL_LENGTH(N'dbo.Media', N'AiJobId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Media] ADD [AiJobId] BIGINT NULL;
END
GO
IF COL_LENGTH(N'dbo.Media', N'LicensePool') IS NULL
BEGIN
    ALTER TABLE [dbo].[Media] ADD [LicensePool] BIT NOT NULL CONSTRAINT [DF_Media_LicensePool] DEFAULT (0);
END
GO

-- Jobs.
IF OBJECT_ID(N'dbo.AiImageJob', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AiImageJob] (
        [Id]               BIGINT IDENTITY(1,1) NOT NULL,
        [GuidId]           UNIQUEIDENTIFIER NOT NULL,
        [CreationTime]     DATETIME2(0) NOT NULL,
        [StartedAt]        DATETIME2(0) NULL,
        [FinishedAt]       DATETIME2(0) NULL,
        [AccountId]        INT NOT NULL,
        [SiteId]           INT NULL,
        [UserId]           INT NOT NULL,
        [ProductId]        INT NULL,
        [SourceMediaId]    INT NULL,
        [SourceUrl]        NVARCHAR(1000) NULL,
        [Look]             NVARCHAR(50) NOT NULL,
        [Stage]            NVARCHAR(20) NULL,
        [Status]           NVARCHAR(20) NOT NULL,
        [Error]            NVARCHAR(1000) NULL,
        [OptionsJson]      NVARCHAR(2000) NULL,
        [IncidentalText]   NVARCHAR(1000) NULL,
        [Provider]         NVARCHAR(30) NULL,
        [Model]            NVARCHAR(80) NULL,
        [Prompt]           NVARCHAR(MAX) NULL,
        [PreviewUrl]       NVARCHAR(1000) NULL,
        [PreviewMediaId]   INT NULL,
        [ResultUrl]        NVARCHAR(1000) NULL,
        [ResultMediaId]    INT NULL,
        [Credits]          INT NOT NULL CONSTRAINT [DF_AiImageJob_Credits] DEFAULT (0),
        [Refunded]         BIT NOT NULL CONSTRAINT [DF_AiImageJob_Refunded] DEFAULT (0),
        [RetryOfJobId]     BIGINT NULL,
        [FidelityScore]    DECIMAL(4,3) NULL,
        [AestheticScore]   DECIMAL(4,3) NULL,
        [JudgeIssues]      NVARCHAR(2000) NULL,
        [JudgeVerdict]     NVARCHAR(20) NULL,
        [InputTokens]      INT NULL,
        [OutputTokens]     INT NULL,
        [ProviderCostUsd]  DECIMAL(10,5) NULL,
        [UserRating]       TINYINT NULL,
        [IsShared]         BIT NOT NULL CONSTRAINT [DF_AiImageJob_IsShared] DEFAULT (0),
        [PreviewMs]        INT NULL,
        [FinalMs]          INT NULL,
        CONSTRAINT [PK_AiImageJob] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AiImageJob_GuidId] ON [dbo].[AiImageJob] ([GuidId]);
    CREATE NONCLUSTERED INDEX [IX_AiImageJob_AccountId_CreationTime] ON [dbo].[AiImageJob] ([AccountId], [CreationTime]);
    CREATE NONCLUSTERED INDEX [IX_AiImageJob_Status] ON [dbo].[AiImageJob] ([Status]);
END
GO
