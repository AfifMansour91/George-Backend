-- Wolt Drive dispatch integration (OC Wolt plugin dispatch API).
-- Run against the George database before deploying the backend update.

IF COL_LENGTH('Site', 'WoltDispatchToken') IS NULL
BEGIN
    ALTER TABLE [Site] ADD [WoltDispatchToken] NVARCHAR(500) NULL;
END
GO

IF COL_LENGTH('[Order]', 'WoltTrackingUrl') IS NULL
BEGIN
    ALTER TABLE [Order] ADD [WoltTrackingUrl] NVARCHAR(1000) NULL;
END
GO

IF COL_LENGTH('[Order]', 'WoltTrackingId') IS NULL
BEGIN
    ALTER TABLE [Order] ADD [WoltTrackingId] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH('[Order]', 'WoltStatus') IS NULL
BEGIN
    ALTER TABLE [Order] ADD [WoltStatus] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH('[Order]', 'WoltDeliveryId') IS NULL
BEGIN
    ALTER TABLE [Order] ADD [WoltDeliveryId] NVARCHAR(64) NULL;
END
GO

IF COL_LENGTH('[Order]', 'WoltDispatchedAt') IS NULL
BEGIN
    ALTER TABLE [Order] ADD [WoltDispatchedAt] DATETIME2(0) NULL;
END
GO

IF COL_LENGTH('[Order]', 'WoltDeliveryJson') IS NULL
BEGIN
    ALTER TABLE [Order] ADD [WoltDeliveryJson] NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('Site', 'WoltEnabled') IS NULL
BEGIN
    ALTER TABLE [Site] ADD [WoltEnabled] BIT NULL;
END
GO
