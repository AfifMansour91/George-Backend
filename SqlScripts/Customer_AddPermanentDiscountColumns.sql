-- Run on George DB before deploying API that maps Customer permanent discount (phone order).
IF COL_LENGTH('dbo.Customer', 'PermanentDiscountType') IS NULL
    ALTER TABLE dbo.Customer ADD PermanentDiscountType NVARCHAR(20) NULL;
IF COL_LENGTH('dbo.Customer', 'PermanentDiscountValue') IS NULL
    ALTER TABLE dbo.Customer ADD PermanentDiscountValue DECIMAL(18, 2) NULL;
