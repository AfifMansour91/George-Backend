-- Adds CustomerPaymentMethod.GatewayProvider ("cardcom" / "payplus"): a saved token is only redeemable
-- at the gateway that issued it, so saved-card flows filter on the site's active provider.
-- All pre-existing rows are Cardcom tokens (PayPlus tokens were never persisted before this column).
-- Run once against the George database. Safe to re-run.

-- Required: the table carries filtered indexes, and sqlcmd defaults to QUOTED_IDENTIFIER OFF.
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.CustomerPaymentMethod', N'GatewayProvider') IS NULL
BEGIN
    ALTER TABLE [dbo].[CustomerPaymentMethod]
        ADD [GatewayProvider] NVARCHAR(32) NOT NULL
        CONSTRAINT DF_CustomerPaymentMethod_GatewayProvider DEFAULT (N'cardcom');
END
GO
