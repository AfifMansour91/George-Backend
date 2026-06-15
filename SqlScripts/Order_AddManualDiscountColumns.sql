-- Run on George DB before deploying API that maps Order manual discount (phone order).
IF COL_LENGTH('dbo.Order', 'ManualDiscountAmount') IS NULL
    ALTER TABLE dbo.[Order] ADD ManualDiscountAmount DECIMAL(18, 2) NULL;
IF COL_LENGTH('dbo.Order', 'ManualDiscountType') IS NULL
    ALTER TABLE dbo.[Order] ADD ManualDiscountType NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Order', 'ManualDiscountValue') IS NULL
    ALTER TABLE dbo.[Order] ADD ManualDiscountValue DECIMAL(18, 2) NULL;
