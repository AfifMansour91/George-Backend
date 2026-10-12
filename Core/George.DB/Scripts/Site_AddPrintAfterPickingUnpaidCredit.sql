-- Adds Site.PrintAfterPickingUnpaidCredit: sub-option of PrintAfterPicking. When true, a credit order whose
-- charge did NOT land at picking finish (declined / timed out / over the hold / finished without a hold) still
-- gets the after-picking voucher printed at finish. NULL/0 = the voucher waits until the charge lands or the
-- payment method changes (current behavior).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'PrintAfterPickingUnpaidCredit') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintAfterPickingUnpaidCredit] BIT NULL;
END
GO
