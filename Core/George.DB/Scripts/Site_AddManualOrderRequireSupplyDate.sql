-- Adds Site.ManualOrderRequireSupplyDate: when true, the new-manual-order screen does NOT
-- auto-select a supply date ("today") and staff must actively pick one (Zano's requested flow).
-- NULL/0 = supply date auto-defaults to today (legacy behavior, restored for all other sites).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ManualOrderRequireSupplyDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ManualOrderRequireSupplyDate] BIT NULL;
END
GO
