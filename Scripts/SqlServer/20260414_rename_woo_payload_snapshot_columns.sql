-- Renames WooCommerce-prefixed payload snapshot columns to neutral names.
-- Run once on databases that already applied an older 20260413 script (WooCommerce* column names).
-- Safe to re-run: each rename runs only if the old column exists and the new name does not.

DECLARE @orderId INT = OBJECT_ID(N'dbo.[Order]', N'U');
DECLARE @itemId INT = OBJECT_ID(N'dbo.OrderItem', N'U');

IF @orderId IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'WooCommerceOrderStatusRaw')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'ExternalOrderStatusRaw')
        EXEC sp_rename N'dbo.[Order].WooCommerceOrderStatusRaw', N'ExternalOrderStatusRaw', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'WooCommercePaymentMethodCode')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'GatewayPaymentMethodCode')
        EXEC sp_rename N'dbo.[Order].WooCommercePaymentMethodCode', N'GatewayPaymentMethodCode', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'WooCommerceShippingStoreName')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'ShippingStoreName')
        EXEC sp_rename N'dbo.[Order].WooCommerceShippingStoreName', N'ShippingStoreName', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'WooCommerceShippingInfoJson')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'ShippingInfoJson')
        EXEC sp_rename N'dbo.[Order].WooCommerceShippingInfoJson', N'ShippingInfoJson', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'WooCommerceShippingAddressJson')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'ShippingAddressJson')
        EXEC sp_rename N'dbo.[Order].WooCommerceShippingAddressJson', N'ShippingAddressJson', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'WooCommerceCustomerJson')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @orderId AND name = N'OrderCustomerJson')
        EXEC sp_rename N'dbo.[Order].WooCommerceCustomerJson', N'OrderCustomerJson', N'COLUMN';
END

IF @itemId IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'WooCommerceSku')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'LineSku')
        EXEC sp_rename N'dbo.OrderItem.WooCommerceSku', N'LineSku', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'WooCommerceQuantityType')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'LineQuantityType')
        EXEC sp_rename N'dbo.OrderItem.WooCommerceQuantityType', N'LineQuantityType', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'WooCommerceUnit')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'LineUnit')
        EXEC sp_rename N'dbo.OrderItem.WooCommerceUnit', N'LineUnit', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'WooCommerceUnitWeightKg')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'LineUnitWeightKg')
        EXEC sp_rename N'dbo.OrderItem.WooCommerceUnitWeightKg', N'LineUnitWeightKg', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'WooCommerceSaleUnitsLine')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'SaleUnitsLine')
        EXEC sp_rename N'dbo.OrderItem.WooCommerceSaleUnitsLine', N'SaleUnitsLine', N'COLUMN';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'WooCommerceLinePayloadJson')
       AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = @itemId AND name = N'LinePayloadJson')
        EXEC sp_rename N'dbo.OrderItem.WooCommerceLinePayloadJson', N'LinePayloadJson', N'COLUMN';
END
