-- Gateway credit orders marked "Paid" by staff with NO charge behind them (found 2026-10-02 while reconciling
-- Hinnawi Jaffa's September revenue report against the acquirer statement).
--
-- What happened: "Paid" is a flag the client sends (ReadyOrderModal "סיום טיפול" = Completed+Paid, and the
-- pre-18/9 "חויב טלפונית" button = CreditPhone+Paid). OrderService.UpdateOrderAsync applied it without checking
-- for a charge. The orders were delivered, the revenue report counted their totals as income, the customers
-- were never charged. Hinnawi #76/#79/#80 alone = 2,154.86 ₪. The code now refuses such a flag
-- (OrderChargeEvidence) and the report excludes such rows; this repairs the rows already written.
--
-- Predicate: Paid, not cancelled, George-gateway credit method, no settled gateway state, no successful
-- ChargeToken/CaptureAuthorization event, no charge transaction id. Woo orders captured by the store plugin
-- have PaymentSettleStatus = Captured and are untouched. Prod 2026-10-02: 10 rows -
--   site 13 (Hinnawi Jaffa): 9601 (#79), 9504 (#76), 9611 (#80)
--   site 25: 6725 (#486), 7545 (#543), 10377 (#741)
--   site 35: 8165 (#89), 8258 (#91)   - Max/CAL token declines, then marked paid
--   site 39: 10751 (#107)
--   site 20: 10307 (#4383)            - SavedCard, authorization hold failed (701)
-- Result: PaymentStatus = Unpaid, PaidAt = NULL. Status (Completed) is kept - the goods were delivered; the
-- order shows "ממתין לתשלום" in the archive so the shop collects (SMS link / manual card / cash switch).
--
-- Run block 1 (preview) and compare with the list above, then block 2 inside the transaction, check the
-- count, COMMIT manually. sqlcmd needs -I (QUOTED_IDENTIFIER ON) for updates on [Order].

SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

-- ===== 1. Preview =====
SELECT o.Id, o.SiteId, o.OrderNumber, o.Source, o.Status, o.PaymentMethod, o.PaymentSettleStatus, o.PaymentGateway,
       o.Total, o.PaidAt, o.UpdateUserId
FROM dbo.[Order] o
WHERE o.IsDeleted = 0
  AND o.PaymentStatus = 'Paid'
  AND o.Status <> 'Cancelled'
  AND o.PaymentGateway IN ('cardcom', 'payplus')
  AND o.PaymentMethod IN ('CreditCard', 'CreditPhone', 'CreditSms', 'SavedCard')
  AND ISNULL(o.PaymentSettleStatus, '') NOT IN ('Captured', 'PartiallyCaptured', 'Refunded', 'PartiallyRefunded')
  AND NOT EXISTS (
        SELECT 1 FROM dbo.OrderPaymentEvent e
        WHERE e.OrderId = o.Id
          AND e.EventType IN ('ChargeToken', 'CaptureAuthorization')
          AND e.StatusCode IN ('0', '000', 'Success'))
  AND o.PaidAt >= '2026-08-15'
ORDER BY o.SiteId, o.PaidAt;

-- ===== 2. Fix (explicit ids from the preview above - re-run the preview first if more time has passed) =====
/*
BEGIN TRAN;

UPDATE o
SET o.PaymentStatus = 'Unpaid',
    o.PaidAt = NULL,
    o.UpdatedDate = GETUTCDATE()
FROM dbo.[Order] o
WHERE o.Id IN (9601, 9504, 9611, 6725, 7545, 10377, 8165, 8258, 10751, 10307)
  AND o.PaymentStatus = 'Paid'
  AND NOT EXISTS (
        SELECT 1 FROM dbo.OrderPaymentEvent e
        WHERE e.OrderId = o.Id
          AND e.EventType IN ('ChargeToken', 'CaptureAuthorization')
          AND e.StatusCode IN ('0', '000', 'Success'));

SELECT @@ROWCOUNT AS UpdatedRows;   -- expect 10

-- Verify, then:
-- COMMIT;
-- ROLLBACK;
*/
