-- Adds Site.ShowOrderHandler: when true, the handler name (מטפל — Order.HandlerName) shows under
-- the order source on the order card and on printouts (voucher or A4).
-- NULL/0 = hidden (default, off).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ShowOrderHandler') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ShowOrderHandler] BIT NULL;
END
GO
