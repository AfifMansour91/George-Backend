-- AI Product Studio, phase 4 "polish" (AI_PRODUCT_STUDIO_SPEC.md §11 row 4): 4K finals, per-site defaults, prompt presets.
-- Run once against the George database BEFORE deploying the backend (after AiStudio_Phase3_Install.sql). Safe to re-run.

------------------------------------------------------------------------------------------------
-- 1. Site defaults (spec §6.4 / §8): the look that starts automatically in the quick-product flow and is preselected in
--    the dialogs, the composition ratio, and whether finals are rendered at 4K (costs HdCredits more per image).
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Site', N'AiDefaultLook') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AiDefaultLook] NVARCHAR(30) NULL;      -- studio_white | dark | marble | ambience | enhance; NULL = first look
    PRINT 'Added AiDefaultLook to Site';
END
GO
IF COL_LENGTH(N'dbo.Site', N'AiImageRatio') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AiImageRatio] NVARCHAR(10) NULL;       -- 1:1 (default) | 4:5 | 3:4 | 4:3 | 16:9
    PRINT 'Added AiImageRatio to Site';
END
GO
IF COL_LENGTH(N'dbo.Site', N'AiFinalSize') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AiFinalSize] NVARCHAR(5) NULL;         -- 2k (default) | 4k
    PRINT 'Added AiFinalSize to Site';
END
GO

------------------------------------------------------------------------------------------------
-- 2. AiStylePreset - the super-admin's prompt tuning per look (spec §8). A row overrides the code defaults in
--    AiStudioLooks for that look: label/description the shops see, the English prompt template ({category} {ratio} {scene}
--    {brief} placeholders), the image provider, the credit price and whether the look is offered at all.
--    AccountId NULL = global (the only kind the admin screen edits today); a per-account row wins over the global one.
------------------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AiStylePreset')
BEGIN
    CREATE TABLE [dbo].[AiStylePreset] (
        [Id] INT IDENTITY (1,1) NOT NULL,
        [AccountId] INT NULL,
        [Look] NVARCHAR(50) NOT NULL,
        [Label] NVARCHAR(100) NULL,
        [Description] NVARCHAR(200) NULL,
        [Prompt] NVARCHAR(MAX) NULL,            -- NULL = code template
        [Provider] NVARCHAR(20) NULL,           -- openai | gemini | NULL = AiStudio:DefaultProvider
        [Credits] INT NULL,                     -- NULL = code price
        [IsActive] BIT NOT NULL CONSTRAINT [DF_AiStylePreset_IsActive] DEFAULT (1),
        [SortOrder] INT NOT NULL CONSTRAINT [DF_AiStylePreset_SortOrder] DEFAULT (0),
        [UpdateTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_AiStylePreset_UpdateTime] DEFAULT (SYSUTCDATETIME()),
        [UpdateUserId] INT NULL,
        CONSTRAINT [PK_AiStylePreset] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
    -- One row per look per scope (SQL Server allows a single NULL AccountId per look in a unique index - exactly what we want).
    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AiStylePreset_Look_Account] ON [dbo].[AiStylePreset]([Look] ASC, [AccountId] ASC);
    PRINT 'Created AiStylePreset';
END
ELSE PRINT 'AiStylePreset already exists';
GO
