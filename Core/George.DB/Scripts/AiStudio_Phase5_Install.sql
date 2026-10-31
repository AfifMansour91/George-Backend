-- AI Product Studio, phase 5 (AI_PRODUCT_STUDIO_SPEC.md §11 row 5): text tone/language per site, reference images and
-- style notes on presets (global + per-account). Run once against the George database BEFORE deploying the backend
-- (after AiStudio_Phase4_Install.sql). Safe to re-run.

------------------------------------------------------------------------------------------------
-- 1. Site: tone + language of the texts "כתוב ב-AI" writes (spec §6.4 "טון טקסט, שפה").
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.Site', N'AiTextTone') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AiTextTone] NVARCHAR(20) NULL;        -- warm (default) | neutral | premium | playful
    PRINT 'Added AiTextTone to Site';
END
GO
IF COL_LENGTH(N'dbo.Site', N'AiTextLanguage') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AiTextLanguage] NVARCHAR(5) NULL;     -- he (default) | en | ar | ru
    PRINT 'Added AiTextLanguage to Site';
END
GO

------------------------------------------------------------------------------------------------
-- 2. AiStylePreset: style reference images per look (JSON array of up to 3 URLs; NULL = the server's AiStudio:ReferenceUrls)
--    and the shop's own style notes (per-account rows; appended to the prompt). The per-account row is now edited in
--    הגדרות › סטודיו AI › המראים שלי (IsActive, StyleNotes, ReferenceUrls); prompt/provider/credits stay super-admin only.
------------------------------------------------------------------------------------------------
IF COL_LENGTH(N'dbo.AiStylePreset', N'ReferenceUrls') IS NULL
BEGIN
    ALTER TABLE [dbo].[AiStylePreset] ADD [ReferenceUrls] NVARCHAR(MAX) NULL;
    PRINT 'Added ReferenceUrls to AiStylePreset';
END
GO
IF COL_LENGTH(N'dbo.AiStylePreset', N'StyleNotes') IS NULL
BEGIN
    ALTER TABLE [dbo].[AiStylePreset] ADD [StyleNotes] NVARCHAR(500) NULL;
    PRINT 'Added StyleNotes to AiStylePreset';
END
GO
