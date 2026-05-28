-- Voucher header: company number (ח.פ) and optional logo on printed vouchers
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'CompanyNumber')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [CompanyNumber] [nvarchar](50) NULL;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'VoucherHeaderShowLogo')
BEGIN
    ALTER TABLE [dbo].[Account]
    ADD [VoucherHeaderShowLogo] [bit] NOT NULL CONSTRAINT [DF_Account_VoucherHeaderShowLogo] DEFAULT (0);
END
GO
