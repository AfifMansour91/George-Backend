-- Auto-send invoice link SMS after payment capture (notification settings → Payments).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]')
      AND name = 'Payment_SendInvoiceSmsAfterCapture'
)
BEGIN
    ALTER TABLE [dbo].[AccountNotificationSettings]
    ADD [Payment_SendInvoiceSmsAfterCapture] [bit] NOT NULL
        CONSTRAINT [DF_AccountNotificationSettings_Payment_SendInvoiceSmsAfterCapture] DEFAULT (1);
    PRINT 'Added Payment_SendInvoiceSmsAfterCapture';
END
GO
