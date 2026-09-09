-- Staff new-order / picking category chips: per-category hide + display order (Hinnawi Jaffa + PEPE, 2026-09-10).
--   ShowInManualOrder       - when 0 the category chip is hidden from the new manual order and picking add-item screens.
--   ManualOrderDisplayOrder - chip order (lower = first); NULL falls back to the kiosk order, then SortOrder.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Category', N'ShowInManualOrder') IS NULL
BEGIN
    ALTER TABLE [dbo].[Category] ADD [ShowInManualOrder] BIT NOT NULL CONSTRAINT DF_Category_ShowInManualOrder DEFAULT 1;
    PRINT 'Added ShowInManualOrder to Category';
END
GO
IF COL_LENGTH(N'dbo.Category', N'ManualOrderDisplayOrder') IS NULL
BEGIN
    ALTER TABLE [dbo].[Category] ADD [ManualOrderDisplayOrder] INT NULL;
    PRINT 'Added ManualOrderDisplayOrder to Category';
END
GO
