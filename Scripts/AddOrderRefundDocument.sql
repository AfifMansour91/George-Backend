-- Refund credit note fields on Order (Cardcom TaxInvoiceAndReceiptRefund)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'RefundInvoiceNumber')
BEGIN
    ALTER TABLE dbo.[Order] ADD RefundInvoiceNumber NVARCHAR(100) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = 'CardcomRefundDocumentUrl')
BEGIN
    ALTER TABLE dbo.[Order] ADD CardcomRefundDocumentUrl NVARCHAR(1000) NULL;
END
GO
