-- Site.ProductPrintNameEnabled: opt-in per site for the product "print name" (Product.PrintName) - the name printed on
-- the ORDER-ENTRY voucher instead of the catalog name. NULL / 0 = off: the field is hidden in the product forms and the
-- vouchers print the catalog name even when a product has a print name. Switched on only for the sites that need it.
-- Idempotent.
IF COL_LENGTH(N'dbo.Site', N'ProductPrintNameEnabled') IS NULL
BEGIN
    ALTER TABLE dbo.Site ADD ProductPrintNameEnabled BIT NULL;
    PRINT 'Site.ProductPrintNameEnabled added';
END
ELSE
    PRINT 'Site.ProductPrintNameEnabled already exists';
GO
