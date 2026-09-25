-- Adds Order.PaymentWallet: the wallet the customer paid with when the gateway reported one
-- (PayPlus alternative_method_name: "apple-pay" / "google-pay" / "bit"). The order card and the payment
-- panel show it instead of the generic "אשראי" (PEPE, 23/9: 33 of 58 PayPlus hosted payments were Apple Pay).
-- Backfills existing PayPlus orders from the stored payment JSON.
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.[Order]', N'PaymentWallet') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PaymentWallet] NVARCHAR(32) NULL;
    PRINT 'Added PaymentWallet to Order';
END
GO

UPDATE o SET o.PaymentWallet = LOWER(w.Wallet)
FROM [dbo].[Order] o
CROSS APPLY (SELECT JSON_VALUE(o.PayPlusPaymentJson, '$.data.alternative_method_name') AS Wallet) w
WHERE o.PaymentWallet IS NULL
  AND o.PayPlusPaymentJson IS NOT NULL
  AND ISJSON(o.PayPlusPaymentJson) = 1
  AND w.Wallet IS NOT NULL
  AND LTRIM(RTRIM(w.Wallet)) <> ''
  AND LOWER(w.Wallet) NOT IN ('null', 'credit-card');
PRINT CONCAT('PaymentWallet backfilled: ', @@ROWCOUNT, ' orders');
GO
