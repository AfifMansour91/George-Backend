-- Audit: full WooCommerce POST /WooCommerce/Order body as JSON (serialized from model; nvarchar(max)).
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'WooCommerceRequestJson')
    ALTER TABLE dbo.[Order] ADD WooCommerceRequestJson NVARCHAR(MAX) NULL;
GO
