/*
  Backfill Order.PaidAt for paid orders missing a charge timestamp.
  Improves דוח הכנסות "לפי חיוב" (no CreationTime fallback needed for these rows).

  Priority:
    1. Earliest successful ChargeToken / CaptureAuthorization payment event
    2. UpdatedDate (when manager marked paid / order was updated)
    3. CreationTime (last resort)

  Run on a DB backup first. Review the preview, then run the UPDATE block.
*/

SET NOCOUNT ON;

-- ---------------------------------------------------------------------------
-- Preview: how many rows will be updated
-- ---------------------------------------------------------------------------
;WITH Candidates AS (
    SELECT
        o.Id,
        o.SiteId,
        o.OrderNumber,
        o.PaymentStatus,
        o.CreationTime,
        o.UpdatedDate,
        pe.FirstChargeEventUtc
    FROM dbo.[Order] o
    OUTER APPLY (
        SELECT MIN(e.CreationTime) AS FirstChargeEventUtc
        FROM dbo.OrderPaymentEvent e
        WHERE e.OrderId = o.Id
          AND e.StatusCode = N'0'
          AND e.EventType IN (N'ChargeToken', N'CaptureAuthorization')
    ) pe
    WHERE o.IsDeleted = 0
      AND o.PaidAt IS NULL
      AND LOWER(LTRIM(RTRIM(o.PaymentStatus))) IN (N'paid', N'refunded')
),
Resolved AS (
    SELECT
        c.*,
        COALESCE(c.FirstChargeEventUtc, c.UpdatedDate, c.CreationTime) AS ProposedPaidAt,
        CASE
            WHEN c.FirstChargeEventUtc IS NOT NULL THEN N'OrderPaymentEvent'
            WHEN c.UpdatedDate IS NOT NULL THEN N'UpdatedDate'
            ELSE N'CreationTime'
        END AS PaidAtSource
    FROM Candidates c
)
SELECT
    COUNT(*) AS RowsToUpdate,
    SUM(CASE WHEN PaidAtSource = N'OrderPaymentEvent' THEN 1 ELSE 0 END) AS FromPaymentEvent,
    SUM(CASE WHEN PaidAtSource = N'UpdatedDate' THEN 1 ELSE 0 END) AS FromUpdatedDate,
    SUM(CASE WHEN PaidAtSource = N'CreationTime' THEN 1 ELSE 0 END) AS FromCreationTime
FROM Resolved;

-- Sample (top 50 by id desc)
;WITH Candidates AS (
    SELECT
        o.Id,
        o.SiteId,
        o.OrderNumber,
        o.PaymentStatus,
        o.CreationTime,
        o.UpdatedDate,
        pe.FirstChargeEventUtc
    FROM dbo.[Order] o
    OUTER APPLY (
        SELECT MIN(e.CreationTime) AS FirstChargeEventUtc
        FROM dbo.OrderPaymentEvent e
        WHERE e.OrderId = o.Id
          AND e.StatusCode = N'0'
          AND e.EventType IN (N'ChargeToken', N'CaptureAuthorization')
    ) pe
    WHERE o.IsDeleted = 0
      AND o.PaidAt IS NULL
      AND LOWER(LTRIM(RTRIM(o.PaymentStatus))) IN (N'paid', N'refunded')
),
Resolved AS (
    SELECT
        c.*,
        COALESCE(c.FirstChargeEventUtc, c.UpdatedDate, c.CreationTime) AS ProposedPaidAt,
        CASE
            WHEN c.FirstChargeEventUtc IS NOT NULL THEN N'OrderPaymentEvent'
            WHEN c.UpdatedDate IS NOT NULL THEN N'UpdatedDate'
            ELSE N'CreationTime'
        END AS PaidAtSource
    FROM Candidates c
)
SELECT TOP (50)
    Id,
    SiteId,
    OrderNumber,
    PaymentStatus,
    PaidAtSource,
    ProposedPaidAt,
    CreationTime,
    UpdatedDate,
    FirstChargeEventUtc
FROM Resolved
ORDER BY Id DESC;

GO

-- ---------------------------------------------------------------------------
-- UPDATE (uncomment after reviewing preview)
-- ---------------------------------------------------------------------------
/*
BEGIN TRANSACTION;

;WITH Candidates AS (
    SELECT
        o.Id,
        pe.FirstChargeEventUtc,
        o.UpdatedDate,
        o.CreationTime
    FROM dbo.[Order] o
    OUTER APPLY (
        SELECT MIN(e.CreationTime) AS FirstChargeEventUtc
        FROM dbo.OrderPaymentEvent e
        WHERE e.OrderId = o.Id
          AND e.StatusCode = N'0'
          AND e.EventType IN (N'ChargeToken', N'CaptureAuthorization')
    ) pe
    WHERE o.IsDeleted = 0
      AND o.PaidAt IS NULL
      AND LOWER(LTRIM(RTRIM(o.PaymentStatus))) IN (N'paid', N'refunded')
)
UPDATE o
SET PaidAt = COALESCE(c.FirstChargeEventUtc, c.UpdatedDate, c.CreationTime)
FROM dbo.[Order] o
INNER JOIN Candidates c ON c.Id = o.Id;

SELECT @@ROWCOUNT AS RowsUpdated;

-- Optional: verify none left without PaidAt among paid/refunded
SELECT COUNT(*) AS StillMissingPaidAt
FROM dbo.[Order]
WHERE IsDeleted = 0
  AND PaidAt IS NULL
  AND LOWER(LTRIM(RTRIM(PaymentStatus))) IN (N'paid', N'refunded');

COMMIT TRANSACTION;
-- ROLLBACK TRANSACTION;
*/
