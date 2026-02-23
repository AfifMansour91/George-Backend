-- Migration: Add AccountNotificationSettings table (1:1 with Account). No JSON column.
-- Run after Migration_AddAccountNotificationSettings.sql if that was used (drops JSON column at end).

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccountNotificationSettings')
BEGIN
    CREATE TABLE [dbo].[AccountNotificationSettings] (
        [Id] [int] IDENTITY(1,1) NOT NULL,
        [AccountId] [int] NOT NULL,
        [IsDeleted] [bit] NOT NULL CONSTRAINT [DF_AccountNotificationSettings_IsDeleted] DEFAULT (0),
        [CreationTime] [datetime2](0) NOT NULL CONSTRAINT [DF_AccountNotificationSettings_CreationTime] DEFAULT (sysutcdatetime()),
        [UpdatedDate] [datetime2](0) NULL,
        [CreationUserId] [int] NULL,
        [UpdateUserId] [int] NULL,
        -- New order
        [NewOrder_ManagerSoundEnabled] [bit] NOT NULL CONSTRAINT [DF_AccountNotificationSettings_NewOrder_ManagerSoundEnabled] DEFAULT (1),
        [NewOrder_ManagerSoundKey] [nvarchar](20) NULL,
        [NewOrder_ManagerSoundTriggerWebsite] [bit] NOT NULL DEFAULT (1),
        [NewOrder_ManagerSoundTriggerKiosk] [bit] NOT NULL DEFAULT (1),
        [NewOrder_ManagerSoundTriggerWhatsapp] [bit] NOT NULL DEFAULT (0),
        [NewOrder_ManagerSoundTriggerPhone] [bit] NOT NULL DEFAULT (0),
        [NewOrder_ManagerMessageChannel] [nvarchar](20) NULL,
        [NewOrder_ManagerPhoneNumbers] [nvarchar](500) NULL,
        [NewOrder_ManagerMessageTemplate] [nvarchar](max) NULL,
        [NewOrder_ManagerReminderBeforeDeliveryEnabled] [bit] NOT NULL DEFAULT (0),
        [NewOrder_ManagerReminderBeforeDeliveryMinutes] [int] NOT NULL DEFAULT (60),
        [NewOrder_ManagerReminderNoTreatmentEnabled] [bit] NOT NULL DEFAULT (0),
        [NewOrder_ManagerReminderNoTreatmentMinutes] [int] NOT NULL DEFAULT (15),
        [NewOrder_ManagerReminderNoTreatmentSoundKey] [nvarchar](20) NULL,
        [NewOrder_CustomerChannel] [nvarchar](20) NULL,
        [NewOrder_CustomerMessageShipping] [nvarchar](max) NULL,
        [NewOrder_CustomerMessagePickup] [nvarchar](max) NULL,
        [NewOrder_CustomerMessageKiosk] [nvarchar](max) NULL,
        -- Order ready
        [OrderReady_ManagerNotifyEnabled] [bit] NOT NULL DEFAULT (0),
        [OrderReady_CustomerChannel] [nvarchar](20) NULL,
        [OrderReady_CustomerMessageShipping] [nvarchar](max) NULL,
        [OrderReady_CustomerMessagePickup] [nvarchar](max) NULL,
        [OrderReady_CustomerMessageKiosk] [nvarchar](max) NULL,
        -- Order not picked up
        [OrderNotPickedUp_ManagerNotifyEnabled] [bit] NOT NULL DEFAULT (0),
        [OrderNotPickedUp_AutoReminderEnabled] [bit] NOT NULL DEFAULT (0),
        [OrderNotPickedUp_MinutesAfterScheduledPickup] [int] NOT NULL DEFAULT (30),
        [OrderNotPickedUp_CustomerMessageTemplate] [nvarchar](max) NULL,
        -- After delivery
        [AfterDelivery_Enabled] [bit] NOT NULL DEFAULT (0),
        [AfterDelivery_TriggerType] [nvarchar](20) NULL,
        [AfterDelivery_TriggerAfterValue] [int] NOT NULL DEFAULT (1),
        [AfterDelivery_TriggerAfterUnit] [nvarchar](20) NULL,
        [AfterDelivery_CustomerMessageTemplate] [nvarchar](max) NULL,
        CONSTRAINT [PK_AccountNotificationSettings] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_AccountNotificationSettings_AccountId] UNIQUE ([AccountId]),
        CONSTRAINT [FK_AccountNotificationSettings_Account] FOREIGN KEY ([AccountId]) REFERENCES [dbo].[Account] ([Id]) ON DELETE CASCADE
    );
    PRINT 'Created AccountNotificationSettings table'
END
ELSE
    PRINT 'AccountNotificationSettings table already exists'
GO

-- Drop JSON column from Account if it was added by a previous migration
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account]') AND name = 'NotificationSettingsJson')
BEGIN
    ALTER TABLE [dbo].[Account] DROP COLUMN [NotificationSettingsJson];
    PRINT 'Dropped NotificationSettingsJson column from Account table'
END
ELSE
    PRINT 'NotificationSettingsJson column does not exist in Account table'
GO
