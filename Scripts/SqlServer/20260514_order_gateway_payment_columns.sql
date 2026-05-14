-- Gateway payment webhook fields (WooCommerce OrderPayment) as first-class columns.
-- Safe to re-run: adds columns only if missing.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'GatewayPaymentOrderId')
    ALTER TABLE [dbo].[Order] ADD [GatewayPaymentOrderId] NVARCHAR(64) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'GatewayPaymentExternalOrderId')
    ALTER TABLE [dbo].[Order] ADD [GatewayPaymentExternalOrderId] NVARCHAR(64) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'GatewayPaymentSiteId')
    ALTER TABLE [dbo].[Order] ADD [GatewayPaymentSiteId] NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'IsFinished')
    ALTER TABLE [dbo].[Order] ADD [IsFinished] NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'GatewayPaymentTransactionId')
    ALTER TABLE [dbo].[Order] ADD [GatewayPaymentTransactionId] NVARCHAR(120) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'PaymentGateway')
    ALTER TABLE [dbo].[Order] ADD [PaymentGateway] NVARCHAR(200) NULL;
