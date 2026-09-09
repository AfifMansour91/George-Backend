-- Two per-site flags requested by Zano Dagim (2026-09-10):
--   PrintCustomerLabelOnNewOrder     - print the customer sticker automatically when a new order arrives
--                                      (server-side, like the new-order voucher; honours CustomerLabelWideFormat).
--   HideUnpaidCreditOrdersInTreatment - keep credit orders off the treatment board until their payment is
--                                      authorized / captured.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'PrintCustomerLabelOnNewOrder') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintCustomerLabelOnNewOrder] BIT NULL;
    PRINT 'Added PrintCustomerLabelOnNewOrder to Site';
END
GO
IF COL_LENGTH(N'dbo.Site', N'HideUnpaidCreditOrdersInTreatment') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [HideUnpaidCreditOrdersInTreatment] BIT NULL;
    PRINT 'Added HideUnpaidCreditOrdersInTreatment to Site';
END
GO
