-- Adds Site.CardcomChargeTerminalNumber: optional SECOND Cardcom terminal, configured at Cardcom WITHOUT
-- a CVV requirement, used ONLY for the actual charge (J4 capture / direct token charge) and its refund.
-- Token creation, authorization holds (J5), voids and the hosted payment page stay on the primary
-- CardcomTerminalNumber. NULL = single-terminal setup (current behavior).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'CardcomChargeTerminalNumber') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [CardcomChargeTerminalNumber] INT NULL;
END
GO
