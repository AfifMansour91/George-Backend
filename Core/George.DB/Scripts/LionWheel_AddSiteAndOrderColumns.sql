-- Delivery-provider abstraction (LionWheel is the first provider):
--   DeliveryProviderConfig  - per-site provider settings (key, trigger status, pickup address, webhook secret)
--   OrderDeliveryDispatch   - per-attempt dispatch history (task id, tracking, status, errors, courier webhook status)
--   Order.DeliveryProvider* - denormalized latest state for cheap list rendering
-- Also drops the earlier single-provider LionWheel columns if a previous version of this script ran.
-- Idempotent - safe to run more than once.

IF OBJECT_ID(N'dbo.DeliveryProviderConfig', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[DeliveryProviderConfig]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_DeliveryProviderConfig] PRIMARY KEY,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_DeliveryProviderConfig_IsDeleted] DEFAULT(0),
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_DeliveryProviderConfig_CreationTime] DEFAULT(SYSUTCDATETIME()),
        [UpdatedDate] DATETIME2(0) NULL,
        [SiteId] INT NOT NULL,
        [ProviderKey] NVARCHAR(30) NOT NULL,
        [Enabled] BIT NOT NULL CONSTRAINT [DF_DeliveryProviderConfig_Enabled] DEFAULT(0),
        [ApiKey] NVARCHAR(500) NULL,
        [TriggerStatus] NVARCHAR(30) NULL,
        [PickupCity] NVARCHAR(120) NULL,
        [PickupStreet] NVARCHAR(200) NULL,
        [PickupNumber] NVARCHAR(30) NULL,
        [PickupName] NVARCHAR(120) NULL,
        [PickupPhone] NVARCHAR(50) NULL,
        [WebhookSecret] NVARCHAR(64) NULL,
        [SettingsJson] NVARCHAR(MAX) NULL,
        CONSTRAINT [FK_DeliveryProviderConfig_Site] FOREIGN KEY ([SiteId]) REFERENCES [dbo].[Site]([Id])
    );
    CREATE UNIQUE INDEX [IX_DeliveryProviderConfig_SiteId_ProviderKey]
        ON [dbo].[DeliveryProviderConfig]([SiteId], [ProviderKey]);
    PRINT 'Created DeliveryProviderConfig';
END

IF OBJECT_ID(N'dbo.OrderDeliveryDispatch', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrderDeliveryDispatch]
    (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_OrderDeliveryDispatch] PRIMARY KEY,
        [IsDeleted] BIT NOT NULL CONSTRAINT [DF_OrderDeliveryDispatch_IsDeleted] DEFAULT(0),
        [CreationTime] DATETIME2(0) NOT NULL CONSTRAINT [DF_OrderDeliveryDispatch_CreationTime] DEFAULT(SYSUTCDATETIME()),
        [UpdatedDate] DATETIME2(0) NULL,
        [OrderId] INT NOT NULL,
        [SiteId] INT NOT NULL,
        [ProviderKey] NVARCHAR(30) NOT NULL,
        [ExternalTaskId] NVARCHAR(64) NULL,
        [TrackingLink] NVARCHAR(500) NULL,
        [Status] NVARCHAR(30) NOT NULL,
        [ErrorMessage] NVARCHAR(500) NULL,
        [AttemptCount] INT NOT NULL CONSTRAINT [DF_OrderDeliveryDispatch_AttemptCount] DEFAULT(0),
        [DispatchedAt] DATETIME2(0) NULL,
        [LastAttemptAt] DATETIME2(0) NULL,
        [CourierStatus] NVARCHAR(50) NULL,
        [CourierStatusUpdatedAt] DATETIME2(0) NULL,
        CONSTRAINT [FK_OrderDeliveryDispatch_Order] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[Order]([Id])
    );
    CREATE INDEX [IX_OrderDeliveryDispatch_OrderId_ProviderKey]
        ON [dbo].[OrderDeliveryDispatch]([OrderId], [ProviderKey]);
    CREATE INDEX [IX_OrderDeliveryDispatch_ProviderKey_ExternalTaskId]
        ON [dbo].[OrderDeliveryDispatch]([ProviderKey], [ExternalTaskId]);
    PRINT 'Created OrderDeliveryDispatch';
END

-- Denormalized latest dispatch state on Order (for order lists).
IF COL_LENGTH(N'dbo.[Order]', N'DeliveryProviderKey') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [DeliveryProviderKey] NVARCHAR(30) NULL;
    PRINT 'Added DeliveryProviderKey to Order';
END

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryProviderTaskId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [DeliveryProviderTaskId] NVARCHAR(64) NULL;
    PRINT 'Added DeliveryProviderTaskId to Order';
END

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryProviderTrackingLink') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [DeliveryProviderTrackingLink] NVARCHAR(500) NULL;
    PRINT 'Added DeliveryProviderTrackingLink to Order';
END

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryProviderStatus') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [DeliveryProviderStatus] NVARCHAR(30) NULL;
    PRINT 'Added DeliveryProviderStatus to Order';
END

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryProviderError') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [DeliveryProviderError] NVARCHAR(500) NULL;
    PRINT 'Added DeliveryProviderError to Order';
END

IF COL_LENGTH(N'dbo.[Order]', N'DeliveryProviderDispatchedAt') IS NULL
BEGIN
    ALTER TABLE [dbo].[Order] ADD [DeliveryProviderDispatchedAt] DATETIME2(0) NULL;
    PRINT 'Added DeliveryProviderDispatchedAt to Order';
END

-- Cleanup: drop the single-provider columns from the earlier (never-deployed) version of this script.
IF COL_LENGTH(N'dbo.Site', N'LionWheelEnabled') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Site] DROP COLUMN
        [LionWheelEnabled], [LionWheelApiKey], [LionWheelTriggerStatus],
        [LionWheelPickupCity], [LionWheelPickupStreet], [LionWheelPickupNumber],
        [LionWheelPickupName], [LionWheelPickupPhone];
    PRINT 'Dropped legacy LionWheel columns from Site';
END

IF COL_LENGTH(N'dbo.[Order]', N'LionWheelTaskId') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[Order] DROP COLUMN
        [LionWheelTaskId], [LionWheelTrackingLink], [LionWheelStatus], [LionWheelDispatchedAt];
    PRINT 'Dropped legacy LionWheel columns from Order';
END
