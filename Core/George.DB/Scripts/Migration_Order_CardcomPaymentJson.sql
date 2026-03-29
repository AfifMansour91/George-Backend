-- WooCommerce POST /WooCommerce/OrderPayment: Cardcom payload JSON + gateway status string.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'CardcomPaymentJson')
    ALTER TABLE dbo.[Order] ADD CardcomPaymentJson NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'ExternalPaymentStatus')
    ALTER TABLE dbo.[Order] ADD ExternalPaymentStatus NVARCHAR(100) NULL;
GO
