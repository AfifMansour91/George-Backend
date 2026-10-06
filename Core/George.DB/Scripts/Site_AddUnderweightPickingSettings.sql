-- Adds the per-site underweight picking alert (PEPE, 2026-10):
--   UnderweightPickingMode             - off / NULL (default, no alert) | confirm (the picker may finish a weighed line
--                                        short after an explicit approval) | block (the line cannot be finished short)
--   UnderweightPickingThresholdPercent - shortage (% of the ordered weight) from which the alert appears; 0 / NULL = any shortage
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'UnderweightPickingMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [UnderweightPickingMode] NVARCHAR(16) NULL;
    PRINT 'Added UnderweightPickingMode to Site';
END
GO

IF COL_LENGTH(N'dbo.Site', N'UnderweightPickingThresholdPercent') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [UnderweightPickingThresholdPercent] DECIMAL(5, 2) NULL;
    PRINT 'Added UnderweightPickingThresholdPercent to Site';
END
GO
