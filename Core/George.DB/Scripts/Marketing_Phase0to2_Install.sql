-- Marketing module (מודול השיווק) - phases 0-2: message log, consent/opt-out, segments, manual sends,
-- deliveries (with short token for click tracking + unsubscribe), attribution, marketing quota bank.
-- Spec: shop-manager/docs/SMS/MARKETING_MODULE_IMPLEMENTATION_PLAN.md
--
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.
-- The module stays hidden per account until Account.MarketingEnabled = 1 (super-admin switch).

-- The filtered unique index on MarketingDelivery.ShortToken needs these ON; sqlcmd defaults QUOTED_IDENTIFIER to OFF.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

------------------------------------------------------------------------------------------------
-- 1. Account gate
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Account', N'MarketingEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account]
        ADD [MarketingEnabled] BIT NOT NULL CONSTRAINT [DF_Account_MarketingEnabled] DEFAULT (0);
    PRINT 'Added MarketingEnabled to Account';
END
GO

-- Inforu support (Migration_AccountSmsSettings.sql, 8/2026) may not have been re-run on a DB whose table predates it:
-- the backend reads Username on every SMS-settings call, so a missing column breaks the whole screen (QA, 1.10).
IF COL_LENGTH(N'dbo.AccountSmsSettings', N'Username') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccountSmsSettings] ADD [Username] NVARCHAR(100) NULL;
    PRINT 'Added Username to AccountSmsSettings';
END
GO

-- One SMS row per (account, provider): the shop's own ActiveTrail and Giorgio's Inforu sub-account coexist,
-- and IsEnabled marks which one is active (none = system SMS account). Replaces the per-account unique index.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_AccountSmsSettings_AccountId' AND object_id = OBJECT_ID(N'dbo.AccountSmsSettings'))
BEGIN
    DROP INDEX [UQ_AccountSmsSettings_AccountId] ON [dbo].[AccountSmsSettings];
    PRINT 'Dropped UQ_AccountSmsSettings_AccountId';
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_AccountSmsSettings_AccountId_Provider' AND object_id = OBJECT_ID(N'dbo.AccountSmsSettings'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AccountSmsSettings_AccountId_Provider]
        ON [dbo].[AccountSmsSettings]([AccountId] ASC, [Provider] ASC);
    PRINT 'Created UQ_AccountSmsSettings_AccountId_Provider';
END
GO

-- Platform-opened SMS sub-accounts: the Inforu sub-account balance is the shop's marketing balance.
IF COL_LENGTH(N'dbo.AccountSmsSettings', N'BilledByPlatform') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccountSmsSettings]
        ADD [BilledByPlatform] BIT NOT NULL CONSTRAINT [DF_AccountSmsSettings_BilledByPlatform] DEFAULT (0);
    PRINT 'Added BilledByPlatform to AccountSmsSettings';
END
GO
IF COL_LENGTH(N'dbo.AccountSmsSettings', N'InforuCustomerId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccountSmsSettings] ADD [InforuCustomerId] NVARCHAR(50) NULL;
    PRINT 'Added InforuCustomerId to AccountSmsSettings';
END
GO

------------------------------------------------------------------------------------------------
-- 2. Customer: consent evidence, opt-out, birth date
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Customer', N'BirthDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[Customer] ADD [BirthDate] DATE NULL;
    PRINT 'Added BirthDate to Customer';
END
GO
IF COL_LENGTH(N'dbo.Customer', N'ConsentSource') IS NULL
BEGIN
    ALTER TABLE [dbo].[Customer] ADD
        [ConsentSource] NVARCHAR(20) NULL,      -- checkout | club | pos | import | manual | legacy
        [ConsentAt] DATETIME2(0) NULL,
        [OptedOutAt] DATETIME2(0) NULL,
        [OptedOutSource] NVARCHAR(20) NULL,     -- link | manual | reply
        [OptedOutDeliveryId] BIGINT NULL;
    PRINT 'Added consent/opt-out columns to Customer';
END
GO
-- Backfill: customers that already approved SMS before consent evidence existed.
UPDATE [dbo].[Customer]
SET [ConsentSource] = N'legacy', [ConsentAt] = [CreationTime]
WHERE [MarketingSms] = 1 AND [ConsentSource] IS NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Customer_AccountId_NormalizedPhone' AND object_id = OBJECT_ID(N'dbo.Customer'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Customer_AccountId_NormalizedPhone]
        ON [dbo].[Customer]([AccountId] ASC, [NormalizedPhone] ASC);
    PRINT 'Created IX_Customer_AccountId_NormalizedPhone';
END
GO

------------------------------------------------------------------------------------------------
-- 3. MessageLog - one row per SMS that leaves the system (operational + marketing)
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MessageLog')
BEGIN
    CREATE TABLE [dbo].[MessageLog] (
        [Id] BIGINT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MessageLog_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [AccountId] INT NULL,
        [SiteId] INT NULL,
        [Kind] NVARCHAR(20) NOT NULL,           -- operational | marketing | system
        [Category] NVARCHAR(40) NOT NULL,       -- order_confirmation | invoice | ... | marketing | otp | test
        [Channel] NVARCHAR(20) NOT NULL CONSTRAINT [DF_MessageLog_Channel] DEFAULT (N'sms'),
        [NormalizedPhone] NVARCHAR(50) NOT NULL,
        [Units] INT NOT NULL,                   -- billable SMS segments
        [Provider] NVARCHAR(30) NULL,
        [UsedAccountConfig] BIT NOT NULL CONSTRAINT [DF_MessageLog_UsedAccountConfig] DEFAULT (0),
        [Success] BIT NOT NULL,
        [Error] NVARCHAR(500) NULL,
        [OrderId] INT NULL,
        [MarketingDeliveryId] BIGINT NULL,
        CONSTRAINT [PK_MessageLog] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    CREATE NONCLUSTERED INDEX [IX_MessageLog_AccountId_CreationTime]
        ON [dbo].[MessageLog]([AccountId] ASC, [CreationTime] ASC) INCLUDE ([Kind], [Category], [Units], [Success]);
    PRINT 'Created MessageLog';
END
ELSE PRINT 'MessageLog already exists';
GO

------------------------------------------------------------------------------------------------
-- 4. MarketingSettings - one row per account (defaults apply when missing)
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingSettings')
BEGIN
    CREATE TABLE [dbo].[MarketingSettings] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [AccountId] INT NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingSettings_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [UpdateTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingSettings_UpdateTime] DEFAULT (SYSUTCDATETIME()),
        [SendWindowStart] NVARCHAR(5) NOT NULL CONSTRAINT [DF_MarketingSettings_SendWindowStart] DEFAULT (N'09:00'),
        [SendWindowEnd] NVARCHAR(5) NOT NULL CONSTRAINT [DF_MarketingSettings_SendWindowEnd] DEFAULT (N'20:00'),
        [BlockShabbatAndHolidays] BIT NOT NULL CONSTRAINT [DF_MarketingSettings_BlockShabbat] DEFAULT (1),
        [FrequencyCapCount] INT NOT NULL CONSTRAINT [DF_MarketingSettings_FrequencyCapCount] DEFAULT (2),
        [FrequencyCapDays] INT NOT NULL CONSTRAINT [DF_MarketingSettings_FrequencyCapDays] DEFAULT (7),
        [AttributionWindowHours] INT NOT NULL CONSTRAINT [DF_MarketingSettings_AttributionWindowHours] DEFAULT (72),
        [SkipOrderedToday] BIT NOT NULL CONSTRAINT [DF_MarketingSettings_SkipOrderedToday] DEFAULT (1),
        CONSTRAINT [PK_MarketingSettings] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingSettings_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id])
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_MarketingSettings_AccountId] ON [dbo].[MarketingSettings]([AccountId] ASC);
    PRINT 'Created MarketingSettings';
END
ELSE PRINT 'MarketingSettings already exists';
GO

------------------------------------------------------------------------------------------------
-- 5. MarketingSegment - saved segments only (the system segments live in code)
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingSegment')
BEGIN
    CREATE TABLE [dbo].[MarketingSegment] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [AccountId] INT NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingSegment_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [UpdateTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingSegment_UpdateTime] DEFAULT (SYSUTCDATETIME()),
        [Name] NVARCHAR(200) NOT NULL,
        [DefinitionJson] NVARCHAR(MAX) NOT NULL,   -- [{axis, operator, value, unit}] - AND only
        [IsPinned] BIT NOT NULL CONSTRAINT [DF_MarketingSegment_IsPinned] DEFAULT (0),
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_MarketingSegment_IsDeleted] DEFAULT (0),
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_MarketingSegment] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingSegment_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_MarketingSegment_AccountId] ON [dbo].[MarketingSegment]([AccountId] ASC);
    PRINT 'Created MarketingSegment';
END
ELSE PRINT 'MarketingSegment already exists';
GO

------------------------------------------------------------------------------------------------
-- 6. MarketingSend - one campaign (manual now; automation runs later)
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingSend')
BEGIN
    CREATE TABLE [dbo].[MarketingSend] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [AccountId] INT NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingSend_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [UpdateTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingSend_UpdateTime] DEFAULT (SYSUTCDATETIME()),
        [Type] NVARCHAR(20) NOT NULL CONSTRAINT [DF_MarketingSend_Type] DEFAULT (N'manual'),   -- manual | automation
        [Name] NVARCHAR(200) NOT NULL,
        [Channel] NVARCHAR(20) NOT NULL CONSTRAINT [DF_MarketingSend_Channel] DEFAULT (N'sms'),
        [Status] NVARCHAR(20) NOT NULL,         -- scheduled | sending | sent | canceled | failed
        [PausedReason] NVARCHAR(20) NULL,       -- no_quota | send_window
        [AudienceType] NVARCHAR(20) NOT NULL,   -- all | segment | filter
        [SegmentId] INT NULL,
        [SystemSegmentKey] NVARCHAR(40) NULL,
        [AudienceLabel] NVARCHAR(300) NULL,
        [AudienceDefinitionJson] NVARCHAR(MAX) NULL,  -- conditions as they were when the send was created
        [SiteIdsJson] NVARCHAR(MAX) NOT NULL,   -- branch scope, e.g. [13,14]
        [Body] NVARCHAR(2000) NOT NULL,
        [LinkUrl] NVARCHAR(1000) NULL,
        [ScheduledAt] DATETIME2(0) NOT NULL,    -- UTC; "send now" = creation time
        [OriginalScheduledAt] DATETIME2(0) NULL,-- set when the send was deferred (Shabbat / quiet hours)
        [StartedAt] DATETIME2(0) NULL,
        [CompletedAt] DATETIME2(0) NULL,
        [AttributionWindowHours] INT NOT NULL CONSTRAINT [DF_MarketingSend_AttributionWindowHours] DEFAULT (72),
        [AudienceCount] INT NOT NULL CONSTRAINT [DF_MarketingSend_AudienceCount] DEFAULT (0),
        [PlannedCount] INT NOT NULL CONSTRAINT [DF_MarketingSend_PlannedCount] DEFAULT (0),
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_MarketingSend] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingSend_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_MarketingSend_AccountId_ScheduledAt] ON [dbo].[MarketingSend]([AccountId] ASC, [ScheduledAt] DESC);
    CREATE NONCLUSTERED INDEX [IX_MarketingSend_Status_ScheduledAt] ON [dbo].[MarketingSend]([Status] ASC, [ScheduledAt] ASC);
    PRINT 'Created MarketingSend';
END
ELSE PRINT 'MarketingSend already exists';
GO

------------------------------------------------------------------------------------------------
-- 7. MarketingDelivery - one row per audience member (also the audience snapshot: skipped rows stay)
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingDelivery')
BEGIN
    CREATE TABLE [dbo].[MarketingDelivery] (
        [Id] BIGINT IDENTITY (1,1) NOT NULL,
        [SendId] INT NOT NULL,
        [AccountId] INT NOT NULL,
        [SiteId] INT NOT NULL,
        [CustomerId] INT NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingDelivery_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [CustomerName] NVARCHAR(200) NULL,
        [NormalizedPhone] NVARCHAR(50) NOT NULL,
        [Status] NVARCHAR(20) NOT NULL,         -- queued | sending | sent | delivered | failed | skipped
        [SkipReason] NVARCHAR(20) NULL,         -- no_consent | no_phone | duplicate | frequency_cap | ordered_today | no_quota | canceled
        [CostUnits] INT NOT NULL CONSTRAINT [DF_MarketingDelivery_CostUnits] DEFAULT (0),
        [ShortToken] NVARCHAR(16) NULL,
        [ClickCount] INT NOT NULL CONSTRAINT [DF_MarketingDelivery_ClickCount] DEFAULT (0),
        [FirstClickedAt] DATETIME2(0) NULL,
        [LastClickedAt] DATETIME2(0) NULL,
        [SentAt] DATETIME2(0) NULL,
        [DeliveredAt] DATETIME2(0) NULL,
        [UnsubscribedAt] DATETIME2(0) NULL,
        [Error] NVARCHAR(500) NULL,
        CONSTRAINT [PK_MarketingDelivery] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingDelivery_Send] FOREIGN KEY ([SendId]) REFERENCES [dbo].[MarketingSend]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_MarketingDelivery_SendId_Status] ON [dbo].[MarketingDelivery]([SendId] ASC, [Status] ASC);
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_MarketingDelivery_ShortToken] ON [dbo].[MarketingDelivery]([ShortToken] ASC) WHERE [ShortToken] IS NOT NULL;
    -- Frequency cap + attribution both look up "what did this phone receive lately in this account".
    CREATE NONCLUSTERED INDEX [IX_MarketingDelivery_AccountId_Phone_SentAt]
        ON [dbo].[MarketingDelivery]([AccountId] ASC, [NormalizedPhone] ASC, [SentAt] ASC) INCLUDE ([Status], [SendId], [LastClickedAt]);
    PRINT 'Created MarketingDelivery';
END
ELSE PRINT 'MarketingDelivery already exists';
GO

------------------------------------------------------------------------------------------------
-- 8. MarketingAttribution - an order credited to a delivery (one attribution per order)
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingAttribution')
BEGIN
    CREATE TABLE [dbo].[MarketingAttribution] (
        [Id] BIGINT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingAttribution_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [AccountId] INT NOT NULL,
        [SendId] INT NOT NULL,
        [DeliveryId] BIGINT NOT NULL,
        [OrderId] INT NOT NULL,
        [CustomerId] INT NULL,
        [Source] NVARCHAR(20) NOT NULL,         -- window (estimated) | coupon (exact - phase 5)
        [WindowHours] INT NOT NULL,
        [Revenue] DECIMAL(18, 2) NOT NULL,
        [OrderCreatedAt] DATETIME2(0) NOT NULL,
        CONSTRAINT [PK_MarketingAttribution] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingAttribution_Send] FOREIGN KEY ([SendId]) REFERENCES [dbo].[MarketingSend]([Id])
    );
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_MarketingAttribution_OrderId] ON [dbo].[MarketingAttribution]([OrderId] ASC);
    CREATE NONCLUSTERED INDEX [IX_MarketingAttribution_SendId] ON [dbo].[MarketingAttribution]([SendId] ASC);
    PRINT 'Created MarketingAttribution';
END
ELSE PRINT 'MarketingAttribution already exists';
GO

------------------------------------------------------------------------------------------------
-- 9. MarketingQuotaLedger - movements journal, not a counter. Balance = SUM(Amount) per bucket.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingQuotaLedger')
BEGIN
    CREATE TABLE [dbo].[MarketingQuotaLedger] (
        [Id] BIGINT IDENTITY (1,1) NOT NULL,
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_MarketingQuotaLedger_CreationTime] DEFAULT (SYSUTCDATETIME()),
        [AccountId] INT NOT NULL,
        [EntryType] NVARCHAR(20) NOT NULL,      -- allocation | purchase | consumption | refund
        [Bucket] NVARCHAR(20) NOT NULL CONSTRAINT [DF_MarketingQuotaLedger_Bucket] DEFAULT (N'bank'),  -- bank | monthly
        [Amount] INT NOT NULL,                  -- positive = in, negative = out
        [Period] NVARCHAR(7) NULL,              -- YYYY-MM, monthly bucket only
        [RefSendId] INT NULL,
        [Note] NVARCHAR(300) NULL,
        [CreationUserId] INT NULL,
        CONSTRAINT [PK_MarketingQuotaLedger] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_MarketingQuotaLedger_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account]([Id])
    );
    CREATE NONCLUSTERED INDEX [IX_MarketingQuotaLedger_AccountId_Bucket] ON [dbo].[MarketingQuotaLedger]([AccountId] ASC, [Bucket] ASC) INCLUDE ([Amount]);
    PRINT 'Created MarketingQuotaLedger';
END
ELSE PRINT 'MarketingQuotaLedger already exists';
GO
