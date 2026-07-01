-- Order refund timestamp: when the (last) refund/credit was performed (set in PaymentService.RefundOrderAsync).
-- Used by the order-detail summary to show "זיכוי (DD/M)".
-- Idempotent: guarded by COL_LENGTH so the script can be re-run safely.

IF COL_LENGTH(N'dbo.[Order]', N'RefundedAt') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD RefundedAt DATETIME2(0) NULL;
END
GO
