-- Add voucher printer settings to Site (Sprint 2: integrations – print by default without confirmation).
-- Run this on the database so the entity columns VoucherPrinterSilent and VoucherPrinterName exist.

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Site')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'VoucherPrinterSilent')
    BEGIN
        ALTER TABLE [dbo].[Site] ADD [VoucherPrinterSilent] [bit] NULL;
        PRINT 'Added Site.VoucherPrinterSilent';
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'VoucherPrinterName')
    BEGIN
        ALTER TABLE [dbo].[Site] ADD [VoucherPrinterName] [nvarchar](100) NULL;
        PRINT 'Added Site.VoucherPrinterName';
    END
END
GO
