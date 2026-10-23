-- Adds Site.HoldNewOrderPrintUntilPaid (Zano Dagim 2026-10-23): when true, the new-order auto prints
-- (VoucherAuto:NewImmediate / FutureImmediate voucher + LabelAuto:NewOrderCustomer sticker) of a CREDIT order
-- wait until the payment is authorized / charged (hosted page completed, J5 hold placed, token charge). An
-- abandoned checkout never prints. Cash / on-account orders print on arrival as before. NULL/0 = today's behavior.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'HoldNewOrderPrintUntilPaid') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [HoldNewOrderPrintUntilPaid] BIT NULL;
    PRINT 'Added HoldNewOrderPrintUntilPaid to Site';
END
GO
