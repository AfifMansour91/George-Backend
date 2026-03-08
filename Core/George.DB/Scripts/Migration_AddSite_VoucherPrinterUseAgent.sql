-- Add Site.VoucherPrinterUseAgent (use local print agent per branch). Default 1 (true).
-- Run this if you only need the Site column (e.g. PrintJob table already exists).
-- Backend: Add property to Site entity and to Site response/request DTOs (e.g. VoucherPrinterUseAgent bool).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Site') AND name = 'VoucherPrinterUseAgent')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [VoucherPrinterUseAgent] BIT NOT NULL CONSTRAINT DF_Site_VoucherPrinterUseAgent DEFAULT 1;
END
ELSE
BEGIN
    -- Column already exists: set default to 1 and update existing rows to 1.
    ALTER TABLE [dbo].[Site] DROP CONSTRAINT IF EXISTS DF_Site_VoucherPrinterUseAgent;
    ALTER TABLE [dbo].[Site] ADD CONSTRAINT DF_Site_VoucherPrinterUseAgent DEFAULT 1 FOR [VoucherPrinterUseAgent];
    UPDATE [dbo].[Site] SET [VoucherPrinterUseAgent] = 1 WHERE [VoucherPrinterUseAgent] = 0;
END
GO
