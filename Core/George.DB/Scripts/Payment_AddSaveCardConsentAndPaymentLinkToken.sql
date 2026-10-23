-- 2026-10-23 payment batch:
--   Site.SaveCardConsentMode        - 'auto' (default, today's behaviour: every card becomes a reusable customer card
--                                     when the site's save-card toggle is on) | 'ask' (only when staff / the customer
--                                     ticked "save my card for future purchases").
--   Order.SaveCardToWallet          - the per-order decision captured when the hosted session was created
--                                     (NULL = legacy order → site toggle). The order-level token for the picking
--                                     charge is kept regardless.
--   Order.PaymentLinkToken          - Giorgio's own /pay/{token} link sent in payment SMS instead of the gateway's
--                                     hosted-page URL (Cardcom links expire). Clicking creates a fresh gateway
--                                     session on demand (Cardcom + PayPlus).
-- Run once against the George database BEFORE deploying the backend. Safe to re-run.

IF COL_LENGTH(N'dbo.Site', N'SaveCardConsentMode') IS NULL
BEGIN
    ALTER TABLE [dbo].[Site] ADD [SaveCardConsentMode] NVARCHAR(10) NOT NULL
        CONSTRAINT [DF_Site_SaveCardConsentMode] DEFAULT ('auto');
    PRINT 'Added SaveCardConsentMode to Site';
END
GO
IF COL_LENGTH(N'dbo.[Order]', N'SaveCardToWallet') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [SaveCardToWallet] BIT NULL;
    PRINT 'Added SaveCardToWallet to Order';
END
GO
IF COL_LENGTH(N'dbo.[Order]', N'PaymentLinkToken') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [PaymentLinkToken] NVARCHAR(40) NULL;
    PRINT 'Added PaymentLinkToken to Order';
END
GO
-- Behaviour preservation: the site toggle CardcomSaveCardEnabled was never enforced before this release, so sites
-- that have it OFF kept saving customer cards anyway (prod 2026-10-23: PEPE site 51 has 131 saved cards and 11
-- saved-card orders with the toggle off). From this release the toggle is enforced - turn it ON wherever cards
-- were actually saved, so nothing changes for them. Sites with the toggle off and no saved cards stay off.
UPDATE s SET s.CardcomSaveCardEnabled = 1
FROM [dbo].[Site] s
WHERE s.CardcomSaveCardEnabled = 0
  AND EXISTS (SELECT 1 FROM [dbo].[CustomerPaymentMethod] m WHERE m.SiteId = s.Id);
PRINT CONCAT('CardcomSaveCardEnabled turned ON for sites that already save cards: ', @@ROWCOUNT);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Order_PaymentLinkToken' AND object_id = OBJECT_ID(N'dbo.[Order]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_Order_PaymentLinkToken] ON [dbo].[Order] ([PaymentLinkToken])
        WHERE [PaymentLinkToken] IS NOT NULL;
    PRINT 'Created IX_Order_PaymentLinkToken';
END
GO
