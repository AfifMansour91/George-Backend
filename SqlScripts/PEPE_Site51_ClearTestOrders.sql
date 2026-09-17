-- PEPE (account 46, site 51) - remove all test orders before go-live. Prepared 2026-09-16, NOT executed.
--
-- Owner's request: every order on the account is a test order; production use starts today.
-- Snapshot at preparation time (16/9): 20 orders (Id 4264..10368, 27/7 -> 15/9), 43 lines, 115 payment
-- events, 55 status rows, 4 print jobs, 0 delivery dispatches, 0 promotion redemptions.
--
-- Approach: SOFT delete (Order.IsDeleted = 1). Every storage query, the reports, the customer stats and the
-- order-number generator filter on IsDeleted, so the orders vanish from the app and the next order gets
-- number 1 - while the payment journal (real PayPlus transactions, invoices 4003/4005, credit note 5003) is
-- kept for support and the change is reversible. Customers and saved cards are untouched.
--
-- BEFORE RUNNING - live PayPlus holds on these test orders (they are not released by deleting the row):
--   order 16 / Id 9816   Authorized 3.13   -> cancel the order in George first (voids the hold), or let it expire
--   order 18 / Id 10280  Initiated  3.13   -> hosted page paid, never applied; the customer-side hold expires on its own
--   order 11 / Id 9489   Initiated  3.13
--   order 7  / Id 5281   Initiated  401.14 (never paid, 9/8)
--
-- Run with sqlcmd -I (QUOTED_IDENTIFIER ON - [Order] has filtered indexes).

-- 1) Preview
SELECT o.Id, o.OrderNumber, o.Status, o.PaymentStatus, o.PaymentSettleStatus, o.Total, o.CustomerName, o.CreationTime
FROM dbo.[Order] o
WHERE o.SiteId = 51 AND o.IsDeleted = 0
ORDER BY o.Id;

SELECT COUNT(*) AS ToDelete FROM dbo.[Order] WHERE SiteId = 51 AND IsDeleted = 0;   -- expect 20

-- 2) Soft delete
BEGIN TRAN;

UPDATE dbo.[Order]
SET IsDeleted = 1,
    UpdatedDate = SYSUTCDATETIME()
WHERE SiteId = 51
  AND AccountId = 46            -- double guard: only the PEPE account
  AND IsDeleted = 0;

SELECT @@ROWCOUNT AS RowsSoftDeleted;   -- expect 20

-- Pending print jobs for those orders must not print later.
UPDATE dbo.PrintJob
SET Status = 'Cancelled'
WHERE OrderId IN (SELECT Id FROM dbo.[Order] WHERE SiteId = 51 AND IsDeleted = 1)
  AND Status IN ('Queued', 'Pending');

SELECT @@ROWCOUNT AS PrintJobsCancelled;

-- 3) Verify: the app must see zero orders and the next order number must be 1
SELECT COUNT(*) AS VisibleOrders FROM dbo.[Order] WHERE SiteId = 51 AND IsDeleted = 0;   -- expect 0

-- Review, then:
-- COMMIT;
-- or ROLLBACK;

-- Undo (if ever needed): UPDATE dbo.[Order] SET IsDeleted = 0 WHERE SiteId = 51 AND IsDeleted = 1 AND UpdatedDate >= '2026-09-16';

-- ---------------------------------------------------------------------------------------------------------
-- Alternative: HARD delete (only if the owner insists the rows physically disappear). Irreversible.
-- Order -> OrderItem / OrderStatusHistory cascade; the rest must go first.
-- BEGIN TRAN;
-- DECLARE @ids TABLE (Id int PRIMARY KEY);
-- INSERT INTO @ids SELECT Id FROM dbo.[Order] WHERE SiteId = 51 AND AccountId = 46;
-- DELETE FROM dbo.PrintJob                 WHERE OrderId IN (SELECT Id FROM @ids);
-- DELETE FROM dbo.PromotionOrderRedemption WHERE OrderId IN (SELECT Id FROM @ids);
-- DELETE FROM dbo.OrderDeliveryDispatch    WHERE OrderId IN (SELECT Id FROM @ids);
-- DELETE FROM dbo.OrderPaymentEvent        WHERE OrderId IN (SELECT Id FROM @ids);
-- DELETE FROM dbo.RealtimeEventLog         WHERE EntityId IN (SELECT Id FROM @ids);
-- DELETE FROM dbo.IntegrationLog           WHERE SiteId = 51 AND EntityType = 'order';
-- DELETE FROM dbo.[Order]                  WHERE Id IN (SELECT Id FROM @ids);   -- cascades OrderItem + OrderStatusHistory
-- SELECT @@ROWCOUNT AS OrdersDeleted;   -- expect 20
-- COMMIT;
