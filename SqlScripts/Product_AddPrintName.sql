-- Product.PrintName: an optional name printed on the ORDER-ENTRY voucher instead of the catalog name
-- (e.g. Arabic text for the kitchen / pickers). NULL = the catalog name. Idempotent.
IF COL_LENGTH(N'dbo.Product', N'PrintName') IS NULL
BEGIN
    ALTER TABLE dbo.Product ADD PrintName NVARCHAR(300) NULL;
    PRINT 'Product.PrintName added';
END
ELSE
    PRINT 'Product.PrintName already exists';
GO
