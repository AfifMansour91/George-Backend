-- Meat Choice 2026-10-23 - two per-site label flags:
--   CustomerLabelWideBrandColumn     - wide (120x80) customer sticker: NULL/1 = keep the pre-printed branded right column
--                                      (Zano's layout); 0 = plain wide label, text starts at the right edge.
--   AfterPickingLabelCustomerDetails - after-picking labels print the customer-details sticker (name, phone, address,
--                                      supply day, payment method) instead of the numbered bag label; one per bag.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'CustomerLabelWideBrandColumn') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [CustomerLabelWideBrandColumn] BIT NULL;
    PRINT 'Added CustomerLabelWideBrandColumn to Site';
END
GO
IF COL_LENGTH(N'dbo.Site', N'AfterPickingLabelCustomerDetails') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [AfterPickingLabelCustomerDetails] BIT NULL;
    PRINT 'Added AfterPickingLabelCustomerDetails to Site';
END
GO
