-- Adds Site.VoucherHideDeliveryTime: when true, order printouts (thermal voucher + A4, manual + auto)
-- omit the delivery/pickup time — the date row shows only the delivery date.
-- NULL/0 = print the time (current behavior).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'VoucherHideDeliveryTime') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [VoucherHideDeliveryTime] BIT NULL;
END
GO
