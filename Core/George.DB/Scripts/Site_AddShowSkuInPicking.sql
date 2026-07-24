-- Adds Site.ShowSkuInPicking: when true, picking-screen lines show the product SKU (מק"ט)
-- under the product name. NULL/0 = hidden (current behavior, off by default).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ShowSkuInPicking') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShowSkuInPicking] BIT NULL;
END
GO
