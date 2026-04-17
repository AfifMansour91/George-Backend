-- Migration: dedicated coupon code(s) on Order for reporting and filtering (ingest fills from WooCommerce payload).

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = N'CouponCode')
    ALTER TABLE [dbo].[Order] ADD [CouponCode] nvarchar(100) NULL;
GO
