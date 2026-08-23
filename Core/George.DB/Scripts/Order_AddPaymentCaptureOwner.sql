-- Adds Order.PaymentCaptureOwner: for website (WooCommerce) orders, who charges the card after picking.
--   NULL / 'Plugin' = the store's Cardcom gateway plugin captures on "completed" and reports by webhook
--                     (legacy flow).
--   'Giorgio'        = the giorgio plugin handed the Cardcom token to Giorgio at checkout; Giorgio charges at
--                     picking (phone-order path) and pushes the payment result back to the store.
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.[Order]', N'PaymentCaptureOwner') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PaymentCaptureOwner] NVARCHAR(20) NULL;
END
GO
