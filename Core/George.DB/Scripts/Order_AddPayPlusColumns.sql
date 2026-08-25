-- Adds PayPlus payment-gateway columns to [Order], parallel to the existing Cardcom* columns.
-- PayPlusTransactionUid alone covers the whole hold-to-capture lifecycle (unlike Cardcom, PayPlus captures
-- the SAME transaction_uid the authorization returned — no separate approval-number/token split needed).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusDocumentUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusDocumentUrl] NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusRefundDocumentUrl') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusRefundDocumentUrl] NVARCHAR(1000) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusPageRequestUid') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusPageRequestUid] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusTransactionUid') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusTransactionUid] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusPaymentJson') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusPaymentJson] NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusCardLast4') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusCardLast4] NVARCHAR(8) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusCardBrand') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusCardBrand] NVARCHAR(32) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'PayPlusSelectedInstallments') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PayPlusSelectedInstallments] INT NULL;
END
GO
