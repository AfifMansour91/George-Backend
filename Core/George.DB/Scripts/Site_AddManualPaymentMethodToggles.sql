-- Adds per-site manual-order payment method toggles (הזמנה חדשה - אמצעי תשלום):
--   PaymentCashEnabled           מזומן             NULL = enabled (legacy default)
--   PaymentCreditSmsEnabled      אשראי ב-SMS       NULL = enabled when Cardcom is configured
--   PaymentCreditPhoneEnabled    אשראי טלפוני       NULL = enabled when Cardcom is configured
--   PaymentExternalCreditEnabled אשראי חיצוני       NULL = enabled (legacy default)
--   PaymentOnAccountEnabled      בהקפה             NULL/0 = hidden (opt-in)
--   PaymentBankTransferEnabled   העברה בנקאית       NULL/0 = hidden (opt-in)
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'PaymentCashEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PaymentCashEnabled] BIT NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PaymentCreditSmsEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PaymentCreditSmsEnabled] BIT NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PaymentCreditPhoneEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PaymentCreditPhoneEnabled] BIT NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PaymentExternalCreditEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PaymentExternalCreditEnabled] BIT NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PaymentOnAccountEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PaymentOnAccountEnabled] BIT NULL;
END
GO

IF COL_LENGTH(N'dbo.Site', N'PaymentBankTransferEnabled') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [PaymentBankTransferEnabled] BIT NULL;
END
GO
