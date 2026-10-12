-- Adds Order.CardcomHoldTerminalNumber: the Cardcom terminal that placed the saved-card J5 hold.
-- Since 2026-10-08 saved-card holds go to the no-CVV charge terminal (CardcomChargeTerminalNumber) instead of
-- the primary; the void (MTI 420) must hit the same terminal, so it is remembered per order.
-- NULL = primary terminal (hosted-page holds, and saved-card holds placed before this change).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.[Order]', N'CardcomHoldTerminalNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [CardcomHoldTerminalNumber] INT NULL;
END
GO
