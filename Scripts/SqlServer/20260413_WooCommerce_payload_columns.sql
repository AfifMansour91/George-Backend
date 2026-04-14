-- Ingest payload snapshots as columns (run once per database).
-- Safe to re-run: adds columns only if missing.
--
-- If this database already has WooCommerce*-prefixed columns from an older revision of
-- this script, run 20260414_rename_woo_payload_snapshot_columns.sql first so you do not
-- add duplicate empty columns alongside the old names.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'ExternalOrderStatusRaw')
    ALTER TABLE [dbo].[Order] ADD [ExternalOrderStatusRaw] NVARCHAR(50) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'GatewayPaymentMethodCode')
    ALTER TABLE [dbo].[Order] ADD [GatewayPaymentMethodCode] NVARCHAR(100) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'ShippingStoreName')
    ALTER TABLE [dbo].[Order] ADD [ShippingStoreName] NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'ShippingInfoJson')
    ALTER TABLE [dbo].[Order] ADD [ShippingInfoJson] NVARCHAR(MAX) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'ShippingAddressJson')
    ALTER TABLE [dbo].[Order] ADD [ShippingAddressJson] NVARCHAR(MAX) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]', N'U') AND name = N'OrderCustomerJson')
    ALTER TABLE [dbo].[Order] ADD [OrderCustomerJson] NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem', N'U') AND name = N'LineSku')
    ALTER TABLE [dbo].[OrderItem] ADD [LineSku] NVARCHAR(120) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem', N'U') AND name = N'LineQuantityType')
    ALTER TABLE [dbo].[OrderItem] ADD [LineQuantityType] NVARCHAR(16) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem', N'U') AND name = N'LineUnit')
    ALTER TABLE [dbo].[OrderItem] ADD [LineUnit] DECIMAL(18, 4) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem', N'U') AND name = N'LineUnitWeightKg')
    ALTER TABLE [dbo].[OrderItem] ADD [LineUnitWeightKg] DECIMAL(18, 4) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem', N'U') AND name = N'SaleUnitsLine')
    ALTER TABLE [dbo].[OrderItem] ADD [SaleUnitsLine] NVARCHAR(200) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.OrderItem', N'U') AND name = N'LinePayloadJson')
    ALTER TABLE [dbo].[OrderItem] ADD [LinePayloadJson] NVARCHAR(MAX) NULL;
