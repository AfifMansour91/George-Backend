-- Adds Order.CardcomSelectedInstallments: installments (תשלומים) the customer selected on the Cardcom
-- hosted page at order creation (J5 hold flow). The post-picking token charge sends this as NumOfPayments.
-- NULL / 1 = single payment (current behavior).
-- Run once against the George database. Safe to re-run.

IF COL_LENGTH(N'dbo.[Order]', N'CardcomSelectedInstallments') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [CardcomSelectedInstallments] INT NULL;
END
GO
