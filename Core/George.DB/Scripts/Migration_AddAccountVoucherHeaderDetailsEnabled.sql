-- When 0: voucher header is legacy single company name line only; when 1: full details block
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'VoucherHeaderDetailsEnabled')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [VoucherHeaderDetailsEnabled] [bit] NOT NULL CONSTRAINT [DF_Account_VoucherHeaderDetailsEnabled] DEFAULT (0);
END
GO
