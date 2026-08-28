-- One-time fix for order 6321 (Zano-Dagim site 45, order number 967, Woo 43809) - 2026-08-20.
--
-- Background: the order's Woo-plugin capture never happened (no plugin capture webhook, no
-- Cardcom document). At 09:54 staff clicked "sync from Cardcom"; Cardcom's GetTransactionInfoById
-- answered with the body "" (a bare JSON string) and the parser read that as a successful final
-- charge, so the order was falsely marked Paid/Captured for 627.40 while only the J5 hold
-- 259462076 (546.50, 19/08) exists. Invoice creation then failed with Cardcom 9999
-- ("לא ניתן להצלב עסקה שנכשלה לקבלה") because tx 259462076 was never charged.
-- The parser is fixed in code (CardcomGateway.ParseTransactionInfoResult); this restores the
-- order's real pre-sync payment state so it can be charged properly.
--
-- BEFORE RUNNING: confirm in the Cardcom dashboard (terminal 191407 / 193477) that no charge of
-- 627.40 exists for this customer on 20/08 - if one exists, do NOT run this; instead set the
-- charge's real transaction id on the order and issue the invoice against it.
--
-- Restores the exact field shape of a pre-capture Authorized website order (mirrors sibling
-- orders 6467/6474/6475/6480/6482/6495). Idempotent via the WHERE guard.

SELECT Id, OrderNumber, PaymentStatus, PaymentSettleStatus, ExternalPaymentStatus, IsFinished,
       PaidAt, PaymentAuthorizedAmount, GatewayPaymentTransactionId, InvoiceNumber, CardcomDocumentUrl
FROM dbo.[Order]
WHERE Id = 6321;

UPDATE dbo.[Order]
SET PaymentStatus = N'Unpaid',
    PaymentSettleStatus = N'Authorized',
    ExternalPaymentStatus = NULL,
    IsFinished = NULL,
    PaidAt = NULL,
    PaymentAuthorizedAmount = 546.50   -- the real J5 hold amount (tx 259462076, 19/08)
WHERE Id = 6321
  AND PaymentSettleStatus = N'Captured'
  AND InvoiceNumber IS NULL
  AND CardcomDocumentUrl IS NULL;      -- refuse to touch the order if an invoice appeared meanwhile
