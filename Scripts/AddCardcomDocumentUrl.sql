-- Cardcom invoice document URL (run if AddPaymentIntegration.sql was already applied)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'CardcomDocumentUrl')
BEGIN
    ALTER TABLE dbo.[Order] ADD CardcomDocumentUrl NVARCHAR(1000) NULL;
END
GO
