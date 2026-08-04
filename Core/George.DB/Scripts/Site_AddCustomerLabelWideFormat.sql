-- Adds Site.CustomerLabelWideFormat: when true, the manual customer sticker (JobType LabelCustomer)
-- is laid out for the wide 120mm pre-printed branded label (order info printed beside the branding
-- column) instead of the default 58x40mm sticker. NULL/0 = default sticker (off by default).
-- The PrintAgent on the site's PC must also be configured with the matching label paper size
-- (LabelPaperWidthMm/LabelPaperHeightMm in the agent settings).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'CustomerLabelWideFormat') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [CustomerLabelWideFormat] BIT NULL;
END
GO
