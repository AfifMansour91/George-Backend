-- Label printer site settings + wire VoucherPrinterUseAgent if missing.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Site') AND name = 'VoucherPrinterUseAgent')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [VoucherPrinterUseAgent] BIT NOT NULL CONSTRAINT DF_Site_VoucherPrinterUseAgent DEFAULT 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Site') AND name = 'LabelPrinterName')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [LabelPrinterName] NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Site') AND name = 'LabelPrinterUseAgent')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [LabelPrinterUseAgent] BIT NOT NULL CONSTRAINT DF_Site_LabelPrinterUseAgent DEFAULT 1;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Site') AND name = 'PrintLabelsAfterPicking')
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PrintLabelsAfterPicking] BIT NULL;
END
GO
