-- Delivery recipient (משלוח עבור אדם אחר): when a website order is placed FOR someone else
-- (OC Woo Shipping "שליחה למישהו אחר" — ocws_recipient_*), the giorgio plugin (>= 1.7.8) sends
-- recipient name/phone in the order payload. George stores them ONLY when the recipient is another
-- person; NULL means the customer receives the order. Shown on the order card, prints and labels so
-- pickers/drivers contact the RECIPIENT, not the orderer (GDBEEF wrong-address incident 15/08/2026).
-- Idempotent: each step is guarded so the script can be re-run safely.

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryRecipientName') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD DeliveryRecipientName NVARCHAR(200) NULL;
END
GO

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryRecipientPhone') IS NULL
BEGIN
    ALTER TABLE dbo.[Order] ADD DeliveryRecipientPhone NVARCHAR(50) NULL;
END
GO
