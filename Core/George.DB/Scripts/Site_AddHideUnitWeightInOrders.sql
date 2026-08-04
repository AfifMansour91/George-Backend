-- Adds Site.HideUnitWeightInOrders: when true, printouts (voucher/A4), order cards and picking
-- lines hide the per-unit / size approximate weight (e.g. "(כ 3 ק"ג)", "700 גרם ליח'").
-- Customer-chosen weights (בחירת משקל ליחידה) always stay visible.
-- NULL/0 = show weights (current behavior, off by default).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'HideUnitWeightInOrders') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [HideUnitWeightInOrders] BIT NULL;
END
GO
