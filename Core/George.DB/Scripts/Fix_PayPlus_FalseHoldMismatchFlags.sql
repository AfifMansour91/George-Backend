-- PayPlus orders falsely flagged "אי-התאמה בחיוב: חויב בפועל 0 ₪" (PEPE, site 51, 2026-09-23/24).
--
-- What happened: George captured each order at picking (CaptureAuthorization event, status 000, amount =
-- order total) under a NEW PayPlus transaction uid, then released the checkout hold remainder (the original
-- approval is now "canceled"). Later, the hosted-page return polling / staff reopening the order asked
-- PayPlus about the checkout page session again; PayPlus described the original approval, and the shared
-- verifier read "approval only, order marked paid, no invoice document" as the false-success signature and
-- wrote GatewayVerifiedAmount = 0 / GatewayAmountMismatch = 1. No invoice existed because Invoice+ had
-- rejected the document (items-total-not-equal-to-calculated-total - discounted orders, fixed separately).
-- The code now verifies the charge transaction in that situation; this repairs the rows already flagged.
--
-- Predicate: PayPlus order, flagged with verified amount 0, settled, and a SUCCESSFUL PayPlus capture event
-- whose amount is within 0.01 of the order total. Prod 2026-09-25: 6 rows (11180, 11534, 11550, 11570,
-- 11583, 11717). Run block 1 (preview), then block 2 inside the transaction, check the count, COMMIT manually.

-- ===== 1. Preview =====
SELECT o.Id, o.SiteId, o.Total, o.GatewayVerifiedAmount, o.GatewayAmountMismatch, o.PaymentSettleStatus,
       cap.Amount AS CapturedAmount, cap.GatewayTransactionId AS ChargeTx, cap.CreationTime AS CapturedAt
FROM dbo.[Order] o
CROSS APPLY (
    SELECT TOP 1 e.Amount, e.GatewayTransactionId, e.CreationTime
    FROM dbo.OrderPaymentEvent e
    WHERE e.OrderId = o.Id AND e.Provider = 'payplus' AND e.EventType = 'CaptureAuthorization' AND e.StatusCode = '0'
    ORDER BY e.Id DESC) cap
WHERE o.PaymentGateway = 'payplus'
  AND o.GatewayAmountMismatch = 1
  AND ISNULL(o.GatewayVerifiedAmount, 0) = 0
  AND o.PaymentSettleStatus IN ('Captured', 'Refunded', 'PartiallyRefunded')
  AND ABS(ISNULL(cap.Amount, 0) - ISNULL(o.Total, 0)) <= 0.01
ORDER BY o.Id;

-- ===== 2. Repair (COMMIT manually after checking the count) =====
BEGIN TRAN;

UPDATE o
SET o.GatewayAmountMismatch = 0,
    o.GatewayVerifiedAmount = cap.Amount,
    o.GatewayVerifiedAt = GETUTCDATE()
FROM dbo.[Order] o
CROSS APPLY (
    SELECT TOP 1 e.Amount
    FROM dbo.OrderPaymentEvent e
    WHERE e.OrderId = o.Id AND e.Provider = 'payplus' AND e.EventType = 'CaptureAuthorization' AND e.StatusCode = '0'
    ORDER BY e.Id DESC) cap
WHERE o.PaymentGateway = 'payplus'
  AND o.GatewayAmountMismatch = 1
  AND ISNULL(o.GatewayVerifiedAmount, 0) = 0
  AND o.PaymentSettleStatus IN ('Captured', 'Refunded', 'PartiallyRefunded')
  AND ABS(ISNULL(cap.Amount, 0) - ISNULL(o.Total, 0)) <= 0.01;

SELECT @@ROWCOUNT AS RepairedOrders; -- expect 6 on prod as of 2026-09-25

-- COMMIT TRAN;
-- ROLLBACK TRAN;
