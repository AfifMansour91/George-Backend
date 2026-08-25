-- Adds PayPlus payment-gateway columns to Site, parallel to the existing Cardcom* columns.
-- PayPlusApiKey/PayPlusSecretKeyEncrypted reuse the same role as Cardcom's ApiName/ApiPasswordEncrypted;
-- PayPlusPaymentPageUid is PayPlus's per-site identifier (no int terminal number concept like Cardcom).
-- Site.PaymentGatewayProvider (existing single string column) already enforces "one gateway per site" —
-- no schema change needed for exclusivity itself.
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'PayPlusPaymentPageUid') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusPaymentPageUid] NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusApiKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusApiKey] NVARCHAR(100) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusSecretKeyEncrypted') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusSecretKeyEncrypted] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusTestMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusTestMode] BIT NOT NULL CONSTRAINT DF_Site_PayPlusTestMode DEFAULT (0);
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusMaxInstallments') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusMaxInstallments] INT NOT NULL CONSTRAINT DF_Site_PayPlusMaxInstallments DEFAULT (1);
END
GO

-- Invoice+ brand UID (issuing business) — required by books/docs/* ("brand-not-found" without it).
IF COL_LENGTH(N'dbo.Site', N'PayPlusInvoiceBrandUid') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusInvoiceBrandUid] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusProviderExtrasJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusProviderExtrasJson] NVARCHAR(2000) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusCssUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusCssUrl] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PayPlusLogoUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PayPlusLogoUrl] NVARCHAR(500) NULL;
END
GO
