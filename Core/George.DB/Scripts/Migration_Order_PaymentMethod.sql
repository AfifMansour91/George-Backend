-- Add PaymentMethod to Order (manual phone + WooCommerce gateway mapping).
-- Run once on existing databases.

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'PaymentMethod')
BEGIN
    ALTER TABLE dbo.[Order] ADD PaymentMethod NVARCHAR(50) NULL;
END
GO
