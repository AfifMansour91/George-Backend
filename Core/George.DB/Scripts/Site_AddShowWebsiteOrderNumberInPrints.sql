-- Adds Site.ShowWebsiteOrderNumberInPrints: when NULL or 1 (default ON), website orders print
-- the website order number (Order.ExternalOrderId) under the "מקור: אתר" line: bare "#N" on the voucher,
-- "מספר הזמנה באתר: #N" in the A4 meta line.
-- 0 = hidden. Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ShowWebsiteOrderNumberInPrints') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShowWebsiteOrderNumberInPrints] BIT NULL;
END
GO
