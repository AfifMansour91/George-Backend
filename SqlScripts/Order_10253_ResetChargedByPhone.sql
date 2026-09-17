-- One-time fix for order 10253 (B12, site 56, order number 21 / Woo 17797) - 2026-09-15.
--
-- Background: picking finished 07:29 UTC. The picking flow released the J5 hold 262657357 (436.00,
-- Cardcom 702 "שיחרור מסגרת בוצע") and then tried a token charge of 482.83 twice (tx 262832291,
-- 262832827) - both declined 60000006 "ת.ז. / תוקף / 3 ספרות בגב הכרטיס שגוי" (Visa / Laumicard;
-- this merchant's terminal declines every no-CVV token charge on Max/Leumi Card cards).
-- At 07:34:15 UTC staff clicked "חויב טלפונית (אשראי)" in the credit panel, which is a plain
-- order update {paymentMethod: CreditPhone, paymentStatus: Paid}. That marks the order Paid with
-- no Cardcom charge behind it, so no invoice was created. Cardcom shows no approved charge.
--
-- BEFORE RUNNING: confirm in the Cardcom dashboard (terminal 196990) that no charge of 482.83
-- exists for this customer on 15/09. If one exists, do NOT run this; set its transaction id on
-- the order and issue the invoice against it instead.
--
-- Restores the pre-click payment state so the order can be paid via a payment link (the same card
-- pays fine on the hosted page - see order 9705 on this site) or switched to cash.
-- Idempotent via the WHERE guard. Run with sqlcmd -I (QUOTED_IDENTIFIER ON).

SELECT Id, OrderNumber, Status, PaymentStatus, PaymentSettleStatus, PaymentMethod, ExternalPaymentStatus,
       PaidAt, PaymentReference, GatewayPaymentTransactionId, InvoiceNumber, CardcomDocumentUrl
FROM dbo.[Order]
WHERE Id = 10253;

BEGIN TRAN;

UPDATE dbo.[Order]
SET PaymentStatus = N'Unpaid',
    PaymentMethod = N'CreditCard',      -- what the WooCommerce order carried before the click
    PaidAt = NULL,
    ExternalPaymentStatus = NULL
WHERE Id = 10253
  AND PaymentStatus = N'Paid'
  AND PaymentMethod = N'CreditPhone'
  AND PaymentSettleStatus = N'Failed'
  AND InvoiceNumber IS NULL
  AND CardcomDocumentUrl IS NULL;      -- refuse to touch the order if an invoice appeared meanwhile

SELECT @@ROWCOUNT AS RowsUpdated;      -- expect 1

-- Review, then:
-- COMMIT;
-- or ROLLBACK;
