-- Adds Site.VoucherHideDeliveryTimeScope: scope of VoucherHideDeliveryTime on order printouts.
--   NULL / 'all'  = hide the time on every order (delivery + pickup) - existing behavior.
--   'shipping'    = hide the time on delivery orders only; pickup orders keep printing their time.
-- Zano 2026-10-07: manual delivery orders carry a forced time slot that the store read as a commitment.
-- Run once against the George database BEFORE deploying the build that reads it. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'VoucherHideDeliveryTimeScope') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [VoucherHideDeliveryTimeScope] NVARCHAR(20) NULL;
END
GO
