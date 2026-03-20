-- WooCommerce: extra order + line fields (labels, notes, saleUnits/saleTotalWeight, WC ids).
-- Run once on existing databases.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'PaymentMethodTitle')
    ALTER TABLE dbo.[Order] ADD PaymentMethodTitle NVARCHAR(200) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'PaymentLabel')
    ALTER TABLE dbo.[Order] ADD PaymentLabel NVARCHAR(150) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'ShippingLabel')
    ALTER TABLE dbo.[Order] ADD ShippingLabel NVARCHAR(150) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'BillingNotes')
    ALTER TABLE dbo.[Order] ADD BillingNotes NVARCHAR(2000) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'InternalOrderNotes')
    ALTER TABLE dbo.[Order] ADD InternalOrderNotes NVARCHAR(MAX) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'WooCommerceSiteId')
    ALTER TABLE dbo.[Order] ADD WooCommerceSiteId NVARCHAR(50) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'WooCommercePickupAffiliateId')
    ALTER TABLE dbo.[Order] ADD WooCommercePickupAffiliateId NVARCHAR(50) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem') AND name = 'SaleUnits')
    ALTER TABLE dbo.OrderItem ADD SaleUnits NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem') AND name = 'SaleTotalWeight')
    ALTER TABLE dbo.OrderItem ADD SaleTotalWeight NVARCHAR(100) NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem') AND name = 'WooCommerceProductId')
    ALTER TABLE dbo.OrderItem ADD WooCommerceProductId INT NULL;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem') AND name = 'WooCommerceVariationId')
    ALTER TABLE dbo.OrderItem ADD WooCommerceVariationId INT NULL;
GO
