-- Adds Site.PickingSortByOrderLines: when true, the picking screen lists items in the order they
-- appear on the order (like the printed voucher) instead of grouped by category.
-- NULL/0 = grouped by category (current behavior, off by default).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'PickingSortByOrderLines') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PickingSortByOrderLines] BIT NULL;
END
GO
