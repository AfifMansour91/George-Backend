-- Cleanup: WooCommerce OrderPayment v2 webhooks used to mark non-gateway (cash etc.) orders
-- as failed payments (PaymentSettleStatus='Failed', ExternalPaymentStatus='failed').
-- The plugin now skips non-Cardcom orders and the backend ignores label-only payment blocks;
-- this script resets orders already polluted by the old behavior.
-- Safe to re-run (idempotent).

SET NOCOUNT ON;

DECLARE @affected TABLE (OrderId INT PRIMARY KEY);

INSERT INTO @affected (OrderId)
SELECT o.Id
FROM dbo.[Order] o
WHERE o.PaymentSettleStatus = N'Failed'
  AND o.ExternalPaymentStatus = N'failed'
  AND o.Source = N'WooCommerce'
  AND (o.GatewayPaymentTransactionId IS NULL OR o.GatewayPaymentTransactionId = N'')
  AND (o.CardcomLowProfileId IS NULL OR o.CardcomLowProfileId = N'')
  AND (o.PaymentGateway IS NULL OR o.PaymentGateway <> N'cardcom');

-- Remove the misleading "WooGatewayPaymentFailed" events shown in the order payment popover.
DELETE e
FROM dbo.OrderPaymentEvent e
JOIN @affected a ON a.OrderId = e.OrderId
WHERE e.EventType = N'WooGatewayPaymentFailed';

PRINT CONCAT('OrderPaymentEvent rows deleted: ', @@ROWCOUNT);

UPDATE o
SET o.PaymentSettleStatus = N'None',
    o.ExternalPaymentStatus = NULL,
    -- PaymentGateway was overwritten with the payment-method label (e.g. N'מזומן').
    o.PaymentGateway = NULL,
    -- Fallback set it to order total even though nothing was authorized.
    o.PaymentAuthorizedAmount = NULL
FROM dbo.[Order] o
JOIN @affected a ON a.OrderId = o.Id;

PRINT CONCAT('Orders reset: ', @@ROWCOUNT);
