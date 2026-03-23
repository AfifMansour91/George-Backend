-- Run on George DB before deploying API that maps Order.DeliveryStreet / DeliveryCity (רחוב+מספר ועיר נפרדים).
IF COL_LENGTH('dbo.Order', 'DeliveryStreet') IS NULL
    ALTER TABLE dbo.[Order] ADD DeliveryStreet NVARCHAR(400) NULL;
IF COL_LENGTH('dbo.Order', 'DeliveryCity') IS NULL
    ALTER TABLE dbo.[Order] ADD DeliveryCity NVARCHAR(120) NULL;
