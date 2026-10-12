-- Adds Site.PickingWeightDecimals: digits after the point for kilograms on the picking screens
-- (ordered / picked columns and the manual weight modal). 2 or 3. NULL = 2 (current behavior).
-- Requested by shops whose scales and price tags work in grams (3 digits, e.g. "3.200 ק"ג").
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'PickingWeightDecimals') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PickingWeightDecimals] INT NULL;
END
GO
