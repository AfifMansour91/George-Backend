-- Marketing: self-service SMS package purchase (מסלולי Giorgio, 15.10.2026).
-- The shop buys a package by itself on the PLATFORM's Cardcom terminal (server config Billing:Cardcom:*), the
-- messages land in its marketing bank (MarketingQuotaLedger) and, for a platform-opened Inforu sub-account, in Inforu.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.
-- QUOTED_IDENTIFIER must be ON: UQ_MarketingPurchase_LowProfileId is a FILTERED index, and every later DML on the table through
-- sqlcmd (default OFF) fails with error 1934 without it. SSMS and SqlClient/EF already run with it ON.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

------------------------------------------------------------------------------------------------
-- 1. MarketingPackage - the global catalog (super-admin edits; prices BEFORE VAT).
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingPackage')
BEGIN
    CREATE TABLE [dbo].[MarketingPackage] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingPackage_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [UpdateTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingPackage_UpdateTime] DEFAULT (SYSUTCDATETIME()),
        [Name] NVARCHAR(100) NULL,
        [Messages] INT NOT NULL,
        [Price] DECIMAL(18, 2) NOT NULL,              -- ILS, before VAT
        [IsActive] BIT NOT NULL CONSTRAINT [DF_MarketingPackage_IsActive] DEFAULT (1),
        [SortOrder] INT NOT NULL CONSTRAINT [DF_MarketingPackage_SortOrder] DEFAULT (0),
        [Badge] NVARCHAR(200) NULL,
        [UpdateUserId] INT NULL,
        CONSTRAINT [PK_MarketingPackage] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    PRINT 'Created MarketingPackage';
END
ELSE PRINT 'MarketingPackage already exists';
GO

-- Seed: the six packages of "SMS שיווקי: חבילות לבחירה" (only when the catalog is empty - never overwrite edits).
IF NOT EXISTS (SELECT 1 FROM [dbo].[MarketingPackage])
BEGIN
    INSERT INTO [dbo].[MarketingPackage] ([Messages], [Price], [SortOrder], [Badge]) VALUES
        (2500,   199.00,  1, NULL),
        (5000,   349.00,  2, NULL),
        (10000,  590.00,  3, NULL),
        (25000,  1290.00, 4, NULL),
        (50000,  2290.00, 5, NULL),
        (100000, 3990.00, 6, NULL);
    PRINT 'Seeded MarketingPackage (6 rows)';
END
GO

------------------------------------------------------------------------------------------------
-- 2. MarketingPurchase - one row per hosted-page session; a paid row owns one ledger entry.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingPurchase')
BEGIN
    CREATE TABLE [dbo].[MarketingPurchase] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingPurchase_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [AccountId] INT NOT NULL,
        [PackageId] INT NULL,
        [PackageName] NVARCHAR(100) NULL,
        [Messages] INT NOT NULL,
        [Price] DECIMAL(18, 2) NOT NULL,
        [VatPercent] DECIMAL(5, 2) NOT NULL,
        [Total] DECIMAL(18, 2) NOT NULL,
        [Currency] NVARCHAR(3) NOT NULL CONSTRAINT [DF_MarketingPurchase_Currency] DEFAULT (N'ILS'),
        [Status] NVARCHAR(20) NOT NULL CONSTRAINT [DF_MarketingPurchase_Status] DEFAULT (N'initiated'),  -- initiated | paid | failed | canceled
        [Gateway] NVARCHAR(20) NOT NULL CONSTRAINT [DF_MarketingPurchase_Gateway] DEFAULT (N'cardcom'),
        [LowProfileId] NVARCHAR(100) NULL,
        [TransactionId] NVARCHAR(100) NULL,
        [ApprovalNumber] NVARCHAR(50) NULL,
        [CardLast4] NVARCHAR(4) NULL,
        [CardBrand] NVARCHAR(50) NULL,
        [DocumentNumber] NVARCHAR(50) NULL,
        [DocumentUrl] NVARCHAR(1000) NULL,
        [PaidAt] DATETIME2(0) NULL,
        [LedgerEntryId] BIGINT NULL,
        [ProviderSyncError] NVARCHAR(500) NULL,
        [ErrorDescription] NVARCHAR(500) NULL,
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_MarketingPurchase] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingPurchase_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id]),
        CONSTRAINT [FK_MarketingPurchase_Package] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[MarketingPackage]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_MarketingPurchase_AccountId_Id] ON [dbo].[MarketingPurchase]([AccountId] ASC, [Id] DESC);
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_MarketingPurchase_LowProfileId] ON [dbo].[MarketingPurchase]([LowProfileId] ASC) WHERE [LowProfileId] IS NOT NULL;
    PRINT 'Created MarketingPurchase';
END
ELSE PRINT 'MarketingPurchase already exists';
GO
