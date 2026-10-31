-- AI Product Studio, phase 3 (AI_PRODUCT_STUDIO_SPEC.md §7, §8, §11): credits ledger, self-service credit packages bought on the
-- PLATFORM's Cardcom terminal (Billing:Cardcom:*, the same terminal as the SMS packages), the community-pool consent stamp and the
-- link from a job to its ledger entry. Run once against the George database BEFORE deploying the backend. Safe to re-run.
-- QUOTED_IDENTIFIER must be ON: UQ_AiCreditPurchase_LowProfileId is a FILTERED index (see Marketing_Packages_Install.sql).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

------------------------------------------------------------------------------------------------
-- 1. Account: when the shop accepted the community-pool terms (community mode only); NULL = not yet.
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Account', N'AiStudioPoolConsentAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account] ADD [AiStudioPoolConsentAt] DATETIME2(0) NULL;
    PRINT 'Added Account.AiStudioPoolConsentAt';
END
GO

------------------------------------------------------------------------------------------------
-- 2. AiImageJob: the consumption ledger entry this job debited (NULL = free / community).
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.AiImageJob', N'LedgerEntryId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AiImageJob] ADD [LedgerEntryId] BIGINT NULL;
    PRINT 'Added AiImageJob.LedgerEntryId';
END
GO

------------------------------------------------------------------------------------------------
-- 3. AiCreditLedger - movements journal, not a counter (pattern: MarketingQuotaLedger). Balance = SUM(Amount).
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AiCreditLedger')
BEGIN
    CREATE TABLE [dbo].[AiCreditLedger] (
        [Id] BIGINT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiCreditLedger_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [AccountId] INT NOT NULL,
        [EntryType] NVARCHAR(20) NOT NULL,      -- allocation | purchase | consumption | refund
        [Amount] INT NOT NULL,                  -- positive = in, negative = out
        [RefJobId] BIGINT NULL,                 -- AiImageJob.Id for consumption / refund
        [RefPurchaseId] INT NULL,               -- AiCreditPurchase.Id for purchase
        [Note] NVARCHAR(300) NULL,
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_AiCreditLedger] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AiCreditLedger_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_AiCreditLedger_AccountId] ON [dbo].[AiCreditLedger]([AccountId] ASC) INCLUDE ([Amount], [EntryType], [CreationTime]);
    PRINT 'Created AiCreditLedger';
END
ELSE PRINT 'AiCreditLedger already exists';
GO

------------------------------------------------------------------------------------------------
-- 4. AiCreditPackage - the global catalog (super-admin edits; prices BEFORE VAT). Pattern: MarketingPackage.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AiCreditPackage')
BEGIN
    CREATE TABLE [dbo].[AiCreditPackage] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiCreditPackage_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [UpdateTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiCreditPackage_UpdateTime] DEFAULT (SYSUTCDATETIME()),
        [Name] NVARCHAR(100) NULL,
        [Credits] INT NOT NULL,
        [Price] DECIMAL(18, 2) NOT NULL,              -- ILS, before VAT
        [IsActive] BIT NOT NULL CONSTRAINT [DF_AiCreditPackage_IsActive] DEFAULT (1),
        [SortOrder] INT NOT NULL CONSTRAINT [DF_AiCreditPackage_SortOrder] DEFAULT (0),
        [Badge] NVARCHAR(200) NULL,
        [UpdateUserId] INT NULL,
        CONSTRAINT [PK_AiCreditPackage] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Created AiCreditPackage';
END
ELSE PRINT 'AiCreditPackage already exists';
GO

-- Seed (only when the catalog is empty - never overwrite edits). Credit = ~₪0.50 before VAT at the base tier: a studio look
-- (4 credits) costs the shop ~₪2 against a provider cost of ~₪1 (spec decision 29.10.2026: price = cost x2).
IF NOT EXISTS (SELECT 1 FROM [dbo].[AiCreditPackage])
BEGIN
    INSERT INTO [dbo].[AiCreditPackage] ([Credits], [Price], [SortOrder], [Badge]) VALUES
        (100,   50.00,  1, NULL),
        (300,   135.00, 2, N'הכי פופולרי'),
        (1000,  400.00, 3, NULL),
        (3000,  1050.00, 4, N'הכי משתלם');
    PRINT 'Seeded AiCreditPackage (4 rows)';
END
GO

------------------------------------------------------------------------------------------------
-- 5. AiCreditPurchase - one row per hosted-page session; a paid row owns one ledger entry. Pattern: MarketingPurchase.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AiCreditPurchase')
BEGIN
    CREATE TABLE [dbo].[AiCreditPurchase] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiCreditPurchase_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [AccountId] INT NOT NULL,
        [PackageId] INT NULL,
        [PackageName] NVARCHAR(100) NULL,
        [Credits] INT NOT NULL,
        [Price] DECIMAL(18, 2) NOT NULL,
        [VatPercent] DECIMAL(5, 2) NOT NULL,
        [Total] DECIMAL(18, 2) NOT NULL,
        [Currency] NVARCHAR(3) NOT NULL CONSTRAINT [DF_AiCreditPurchase_Currency] DEFAULT (N'ILS'),
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_AiCreditPurchase_Status] DEFAULT (N'initiated'),  -- initiated | paid | failed | canceled
        [Gateway] NVARCHAR(20) NOT NULL CONSTRAINT [DF_AiCreditPurchase_Gateway] DEFAULT (N'cardcom'),
        [LowProfileId] NVARCHAR(100) NULL,
        [TransactionId] NVARCHAR(100) NULL,
        [ApprovalNumber] NVARCHAR(50) NULL,
        [CardLast4] NVARCHAR(4) NULL,
        [CardBrand] NVARCHAR(50) NULL,
        [DocumentNumber] NVARCHAR(50) NULL,
        [DocumentUrl] NVARCHAR(1000) NULL,
        [PaidAt] DATETIME2(0) NULL,
        [LedgerEntryId] BIGINT NULL,
        [ErrorDescription] NVARCHAR(500) NULL,
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_AiCreditPurchase] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AiCreditPurchase_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id]),
        CONSTRAINT [FK_AiCreditPurchase_Package] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[AiCreditPackage]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_AiCreditPurchase_AccountId_Id] ON [dbo].[AiCreditPurchase]([AccountId] ASC, [Id] DESC);
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AiCreditPurchase_LowProfileId] ON [dbo].[AiCreditPurchase]([LowProfileId] ASC) WHERE [LowProfileId] IS NOT NULL;
    PRINT 'Created AiCreditPurchase';
END
ELSE PRINT 'AiCreditPurchase already exists';
GO

------------------------------------------------------------------------------------------------
-- 6. AiProviderSpend - one row per paid provider call (OpenAI / Gemini image, Anthropic vision), in USD. This is the
--    platform's own cost journal per provider: no provider exposes a balance API, so the wallet panel on /aistudioadmin is
--    "balance the admin typed in" minus SUM(AmountUsd) since then.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AiProviderSpend')
BEGIN
    CREATE TABLE [dbo].[AiProviderSpend] (
        [Id] BIGINT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiProviderSpend_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [Provider] NVARCHAR(20) NOT NULL,       -- openai | gemini | anthropic
        [Model] NVARCHAR(100) NULL,
        [Kind] NVARCHAR(20) NOT NULL,           -- identify | brief | judge | preview | final | describe
        [AccountId] INT NULL,
        [RefJobId] BIGINT NULL,
        [InputTokens] INT NULL,
        [OutputTokens] INT NULL,
        [AmountUsd] DECIMAL(12, 5) NOT NULL,
        CONSTRAINT [PK_AiProviderSpend] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_AiProviderSpend_Provider_CreationTime] ON [dbo].[AiProviderSpend]([Provider] ASC, [CreationTime] ASC) INCLUDE ([AmountUsd]);
    PRINT 'Created AiProviderSpend';
END
ELSE PRINT 'AiProviderSpend already exists';
GO

------------------------------------------------------------------------------------------------
-- 7. AiProviderTopUp - the super-admin writes down the balance shown in the provider console (after loading money).
--    Latest row per provider is the anchor; remaining = BalanceUsd - spend since CreationTime.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AiProviderTopUp')
BEGIN
    CREATE TABLE [dbo].[AiProviderTopUp] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiProviderTopUp_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [Provider] NVARCHAR(20) NOT NULL,
        [BalanceUsd] DECIMAL(12, 2) NOT NULL,   -- balance in the console at that moment (not the amount loaded)
        [AlertBelowUsd] DECIMAL(12, 2) NULL,    -- warn on the admin page under this (NULL = default 20)
        [Note] NVARCHAR(300) NULL,
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_AiProviderTopUp] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_AiProviderTopUp_Provider_Id] ON [dbo].[AiProviderTopUp]([Provider] ASC, [Id] DESC);
    PRINT 'Created AiProviderTopUp';
END
ELSE PRINT 'AiProviderTopUp already exists';
GO
