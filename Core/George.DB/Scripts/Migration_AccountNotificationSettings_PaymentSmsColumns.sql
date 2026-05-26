-- Payment / invoice SMS templates (סליקה, חשבונית, זיכוי, קישור תשלום).
-- Default message text matches PaymentNotificationDefaults.cs / notificationsApi.ts (KSP-style).

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountNotificationSettings')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'Payment_CustomerMessageInvoice')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [Payment_CustomerMessageInvoice] [nvarchar](max) NULL;
        PRINT 'Added Payment_CustomerMessageInvoice'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'Payment_CustomerMessageRefund')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [Payment_CustomerMessageRefund] [nvarchar](max) NULL;
        PRINT 'Added Payment_CustomerMessageRefund'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'Payment_CustomerMessagePaymentLink')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [Payment_CustomerMessagePaymentLink] [nvarchar](max) NULL;
        PRINT 'Added Payment_CustomerMessagePaymentLink'
    END
END
GO

-- Seed KSP-style defaults for existing accounts (only where column is NULL / empty; does not overwrite custom text).
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountNotificationSettings')
   AND EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'Payment_CustomerMessageInvoice')
BEGIN
    DECLARE @DefaultInvoice NVARCHAR(MAX) = N'התקבלה חשבונית מס וקבלה דיגיטליים מספר [invoice_number] מאת [store_name]: [document_url]';
    DECLARE @DefaultRefund NVARCHAR(MAX) = N'התקבל זיכוי מאת [store_name], לצפייה בסכום הזיכוי והחשבונית מס זיכוי דיגיטלית נא לפתוח את הקישור: [document_url]';
    DECLARE @DefaultPaymentLink NVARCHAR(MAX) = N'לתשלום עבור הזמנה [order_number] מאת [store_name]: [payment_url]';

    UPDATE [dbo].[AccountNotificationSettings]
    SET [Payment_CustomerMessageInvoice] = @DefaultInvoice,
        [UpdatedDate] = SYSUTCDATETIME()
    WHERE [IsDeleted] = 0
      AND ([Payment_CustomerMessageInvoice] IS NULL OR LTRIM(RTRIM([Payment_CustomerMessageInvoice])) = N'');

    UPDATE [dbo].[AccountNotificationSettings]
    SET [Payment_CustomerMessageRefund] = @DefaultRefund,
        [UpdatedDate] = SYSUTCDATETIME()
    WHERE [IsDeleted] = 0
      AND ([Payment_CustomerMessageRefund] IS NULL OR LTRIM(RTRIM([Payment_CustomerMessageRefund])) = N'');

    UPDATE [dbo].[AccountNotificationSettings]
    SET [Payment_CustomerMessagePaymentLink] = @DefaultPaymentLink,
        [UpdatedDate] = SYSUTCDATETIME()
    WHERE [IsDeleted] = 0
      AND ([Payment_CustomerMessagePaymentLink] IS NULL OR LTRIM(RTRIM([Payment_CustomerMessagePaymentLink])) = N'');

    PRINT 'Seeded default payment SMS templates where empty'
END
GO
