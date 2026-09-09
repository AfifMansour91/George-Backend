-- Adds Customer.InvoiceName / Customer.InvoiceTaxId ("חשבונית על שם אחר"): a business name + ח.פ printed on
-- the customer's invoices instead of the personal name (Zano Dagim 2026-09-09). Null = current behavior.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Customer', N'InvoiceName') IS NULL
BEGIN
    ALTER TABLE [dbo].[Customer] ADD [InvoiceName] NVARCHAR(200) NULL;
    PRINT 'Added InvoiceName to Customer';
END
GO
IF COL_LENGTH(N'dbo.Customer', N'InvoiceTaxId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Customer] ADD [InvoiceTaxId] NVARCHAR(32) NULL;
    PRINT 'Added InvoiceTaxId to Customer';
END
GO
