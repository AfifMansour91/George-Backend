-- Adds Site.CardcomMaxInstallments: max installments (תשלומים) offered on the Cardcom hosted
-- payment page for IMMEDIATE charges only. 1 = single payment, selector hidden (current behavior).
-- J5 authorization holds and direct token charges (picking-time charge, retries, refunds) always
-- stay single-payment regardless of this value.
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'CardcomMaxInstallments') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site]
        ADD [CardcomMaxInstallments] INT NOT NULL
            CONSTRAINT [DF_Site_CardcomMaxInstallments] DEFAULT (1);
END
GO
