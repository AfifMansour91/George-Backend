-- Adds CustomerPaymentMethod.GatewayCustomerId: the gateway-side customer id a saved token is bound to.
-- PayPlus requires customer_uid next to the token on Transactions/Charge|Approval (use_token=true), so a
-- saved PayPlus card is unusable without it (PEPE 9/9: "saved card cannot be used"). Null for Cardcom.
-- Existing PayPlus rows stay null - those cards must be saved again through the payment page.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.CustomerPaymentMethod', N'GatewayCustomerId') IS NULL
BEGIN
    ALTER TABLE [dbo].[CustomerPaymentMethod] ADD [GatewayCustomerId] NVARCHAR(64) NULL;
    PRINT 'Added GatewayCustomerId to CustomerPaymentMethod';
END
GO
