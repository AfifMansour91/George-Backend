-- Run on George DB before deploying API that maps OrderItem.OrderLine* (shop-manager order line Hebrew display).
IF COL_LENGTH('dbo.OrderItem', 'OrderLineQuantityMode') IS NULL
    ALTER TABLE dbo.OrderItem ADD OrderLineQuantityMode NVARCHAR(16) NULL;
IF COL_LENGTH('dbo.OrderItem', 'OrderLinePerUnitWeightLabel') IS NULL
    ALTER TABLE dbo.OrderItem ADD OrderLinePerUnitWeightLabel NVARCHAR(120) NULL;
IF COL_LENGTH('dbo.OrderItem', 'OrderLineSizeLabel') IS NULL
    ALTER TABLE dbo.OrderItem ADD OrderLineSizeLabel NVARCHAR(200) NULL;
IF COL_LENGTH('dbo.OrderItem', 'OrderLineCuttingLabel') IS NULL
    ALTER TABLE dbo.OrderItem ADD OrderLineCuttingLabel NVARCHAR(200) NULL;
