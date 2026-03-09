-- Site: Internal API key for external integrations (e.g. WooCommerce) to call our APIs (X-Api-Key).
-- Order: CustomerEmail, PaymentReference, InvoiceNumber, PaidAt for payment flow.

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Site')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Site]') AND name = 'InternalApiKey')
    BEGIN
        ALTER TABLE [dbo].[Site] ADD [InternalApiKey] [nvarchar](100) NULL;
        PRINT 'Added InternalApiKey to Site'
    END
END
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Order')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = 'CustomerEmail')
    BEGIN
        ALTER TABLE [dbo].[Order] ADD [CustomerEmail] [nvarchar](200) NULL;
        PRINT 'Added CustomerEmail to Order'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = 'PaymentReference')
    BEGIN
        ALTER TABLE [dbo].[Order] ADD [PaymentReference] [nvarchar](100) NULL;
        PRINT 'Added PaymentReference to Order'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = 'InvoiceNumber')
    BEGIN
        ALTER TABLE [dbo].[Order] ADD [InvoiceNumber] [nvarchar](100) NULL;
        PRINT 'Added InvoiceNumber to Order'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Order]') AND name = 'PaidAt')
    BEGIN
        ALTER TABLE [dbo].[Order] ADD [PaidAt] [datetime2](0) NULL;
        PRINT 'Added PaidAt to Order'
    END
END
GO
