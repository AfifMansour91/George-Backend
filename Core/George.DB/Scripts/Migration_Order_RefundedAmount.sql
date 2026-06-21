-- Cumulative refund amount per order (partial + full credits).
IF COL_LENGTH('dbo.[Order]', 'RefundedAmount') IS NULL
BEGIN
    ALTER TABLE dbo.[Order]
        ADD RefundedAmount DECIMAL(18, 2) NULL;
END
GO

-- Backfill from successful Cardcom refund payment events.
UPDATE o
SET o.RefundedAmount = agg.Total
FROM dbo.[Order] o
INNER JOIN (
    SELECT e.OrderId, SUM(ISNULL(e.Amount, 0)) AS Total
    FROM dbo.OrderPaymentEvent e
    WHERE e.EventType = N'Refund'
      AND e.StatusCode IN (N'0', N'000', N'Success')
    GROUP BY e.OrderId
) agg ON agg.OrderId = o.Id
WHERE agg.Total > 0
  AND (o.RefundedAmount IS NULL OR o.RefundedAmount = 0);
GO
