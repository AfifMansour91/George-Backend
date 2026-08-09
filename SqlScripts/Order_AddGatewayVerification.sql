-- Cardcom charge verification for website orders (PaymentService.TryVerifyWooGatewayChargeAsync):
-- GatewayVerifiedAmount = the amount Cardcom actually reports for the stored transaction,
-- GatewayVerifiedAt    = when the inquiry last produced a verdict,
-- GatewayAmountMismatch = 1 when Cardcom's charge diverges from the order's expected net
--                         (Total − RefundedAmount); NULL = not verified yet.
-- Idempotent: guarded by COL_LENGTH so the script can be re-run safely.

IF COL_LENGTH(N'dbo.[Order]', N'GatewayVerifiedAmount') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD GatewayVerifiedAmount DECIMAL(18, 2) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'GatewayVerifiedAt') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD GatewayVerifiedAt DATETIME2(0) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'GatewayAmountMismatch') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD GatewayAmountMismatch BIT NULL;
END
GO
