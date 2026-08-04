-- Adds Site.ConfirmDeliveryFeePopup: when true, selecting home delivery on a manual/phone order
-- opens a confirmation popup showing the delivery city and an editable delivery fee for that order.
-- NULL/0 = disabled (current behavior, off by default).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'ConfirmDeliveryFeePopup') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [ConfirmDeliveryFeePopup] BIT NULL;
END
GO
