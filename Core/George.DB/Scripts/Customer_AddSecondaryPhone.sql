-- Adds Customer.SecondaryPhone (2026-10-23): a second phone that may receive the payment-link SMS instead of the
-- customer's own (the spouse who pays). Learned when staff send the link "למספר אחר" and tick "save"; offered as a
-- choice the next time the customer orders with credit-by-SMS. NULL = no secondary number.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Customer', N'SecondaryPhone') IS NULL
BEGIN
    ALTER TABLE [dbo].[Customer] ADD [SecondaryPhone] NVARCHAR(50) NULL;
    PRINT 'Added SecondaryPhone to Customer';
END
GO
