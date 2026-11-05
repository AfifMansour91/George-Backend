-- Adds Site.MissingInvoiceAlertEnabled / MissingInvoiceAlertMinutes (PEPE, 4.11.2026): when enabled, a card order
-- (PayPlus/Cardcom) that is Paid + Captured but still has no invoice document N minutes after payment raises one
-- alert - an in-app notification to the site's open sessions and an SMS to the manager phones of the
-- notification settings. One alert per order (journaled as OrderPaymentEvent 'MissingInvoiceAlert').
-- NULL = off. Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'MissingInvoiceAlertEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [MissingInvoiceAlertEnabled] BIT NULL;
END
GO
IF COL_LENGTH(N'dbo.Site', N'MissingInvoiceAlertMinutes') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [MissingInvoiceAlertMinutes] INT NULL;   -- default 10 when NULL
END
GO
