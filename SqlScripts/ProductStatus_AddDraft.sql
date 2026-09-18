-- Bundles (מארזים): the bundle editor offers "טיוטה" (draft) so a bundle can be built before it goes live.
-- ProductStatus had no 'draft' row, so the status lookup returned NULL, the product was saved as "no status"
-- and the WooCommerce sync (which already maps a 'draft' status name to Woo `draft`) published it.
-- Idempotent.

IF NOT EXISTS (SELECT 1 FROM dbo.ProductStatus WHERE Name = N'draft')
BEGIN
    INSERT INTO dbo.ProductStatus (Name, IsDeleted) VALUES (N'draft', 0);
END
GO
