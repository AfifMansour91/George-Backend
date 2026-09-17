-- One-time fix for order 10279 (PEPE, site 51, order number 17) - 2026-09-16.
--
-- Background: PayPlus Invoice+ answers document creation with {"status":"success","details":{docUID,number,
-- originalDocAddress}}. PayPlusGateway.CreateDocumentAsync looked for docUID at the root, so every real success
-- was logged as "PayPlus document creation failed." and nothing was stored on the order. Parser fixed in code.
-- Journal (OrderPaymentEvent.RawResponseJson) proves PayPlus created:
--   20:40:19 UTC  invoice  4003  (event 21405)  <- the real one for the 2.95 capture
--   21:03:02 UTC  invoice  4004  (event 21407)  <- DUPLICATE created by the manual retry - cancel it in PayPlus
--   21:04:27 UTC  unique-identifier-exists      (event 21408)
--   21:25:47 UTC  refund   5003  (event 21410)  <- credit note for the 2.95 refund
-- The customer got no invoice SMS (InvoiceSms Skipped: missing document URL).
--
-- Attaches invoice 4003 and credit note 5003 to the order. Run with sqlcmd -I (QUOTED_IDENTIFIER ON).

SELECT Id, OrderNumber, PaymentStatus, PaymentSettleStatus, RefundedAmount, InvoiceNumber, PayPlusDocumentUrl, RefundInvoiceNumber, PayPlusRefundDocumentUrl
FROM dbo.[Order] WHERE Id = 10279;

BEGIN TRAN;

UPDATE dbo.[Order]
SET InvoiceNumber = N'4003',
    PayPlusDocumentUrl = N'https://restapi.payplus.co.il/getdoc/s/o/c6d98ea5-617d-4afe-93aa-f002fdd38d37.pdf',
    RefundInvoiceNumber = N'5003',
    PayPlusRefundDocumentUrl = N'https://restapi.payplus.co.il/getdoc/s/o/c05f52d1-308d-450c-a5d5-401734319c7d.pdf'
WHERE Id = 10279
  AND InvoiceNumber IS NULL
  AND PayPlusDocumentUrl IS NULL;

SELECT @@ROWCOUNT AS RowsUpdated;      -- expect 1

-- Review, then:
-- COMMIT;
-- or ROLLBACK;
