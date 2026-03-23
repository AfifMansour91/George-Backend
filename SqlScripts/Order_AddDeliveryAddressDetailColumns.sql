-- Run on George DB before deploying API that maps Order.DeliveryApartment / DeliveryFloor / DeliveryEntranceCode (shop-manager manual order + delivery form).
IF COL_LENGTH('dbo.Order', 'DeliveryApartment') IS NULL
    ALTER TABLE dbo.[Order] ADD DeliveryApartment NVARCHAR(64) NULL;
IF COL_LENGTH('dbo.Order', 'DeliveryFloor') IS NULL
    ALTER TABLE dbo.[Order] ADD DeliveryFloor NVARCHAR(32) NULL;
IF COL_LENGTH('dbo.Order', 'DeliveryEntranceCode') IS NULL
    ALTER TABLE dbo.[Order] ADD DeliveryEntranceCode NVARCHAR(64) NULL;
