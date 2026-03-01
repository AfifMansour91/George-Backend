-- Add phone-order SMS columns to AccountNotificationSettings (Sprint 2: SMS ללקוח for manual/phone orders).
-- Run this on the database so the entity columns NewOrder_CustomerSmsOnPhoneOrderEnabled and NewOrder_CustomerMessagePhoneOrder exist.

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountNotificationSettings')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'NewOrder_CustomerSmsOnPhoneOrderEnabled')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [NewOrder_CustomerSmsOnPhoneOrderEnabled] [bit] NOT NULL CONSTRAINT [DF_AccountNotificationSettings_NewOrder_CustomerSmsOnPhoneOrderEnabled] DEFAULT (0);
        PRINT 'Added NewOrder_CustomerSmsOnPhoneOrderEnabled'
    END
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountNotificationSettings]') AND name = 'NewOrder_CustomerMessagePhoneOrder')
    BEGIN
        ALTER TABLE [dbo].[AccountNotificationSettings] ADD [NewOrder_CustomerMessagePhoneOrder] [nvarchar](max) NULL;
        PRINT 'Added NewOrder_CustomerMessagePhoneOrder'
    END
END
GO
