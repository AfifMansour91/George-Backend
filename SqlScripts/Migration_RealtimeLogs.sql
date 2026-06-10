-- Generic SignalR diagnostics (all hubs / features).
-- Replaces order-specific OrderRealtimeHubLog / OrderRealtimePushLog if those were created earlier.

IF OBJECT_ID(N'dbo.OrderRealtimeHubLog', N'U') IS NOT NULL
    DROP TABLE dbo.OrderRealtimeHubLog;
GO
IF OBJECT_ID(N'dbo.OrderRealtimePushLog', N'U') IS NOT NULL
    DROP TABLE dbo.OrderRealtimePushLog;
GO

IF OBJECT_ID(N'dbo.RealtimeHubLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RealtimeHubLog (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        HubName NVARCHAR(64) NOT NULL,
        Feature NVARCHAR(64) NULL,
        EventType NVARCHAR(32) NOT NULL,
        ConnectionId NVARCHAR(128) NOT NULL,
        UserId INT NULL,
        SiteId INT NULL,
        AccountId INT NULL,
        Detail NVARCHAR(500) NULL,
        CreationTime DATETIME2(0) NOT NULL CONSTRAINT DF_RealtimeHubLog_CreationTime DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_RealtimeHubLog_Hub_CreationTime ON dbo.RealtimeHubLog (HubName, CreationTime DESC);
    CREATE INDEX IX_RealtimeHubLog_UserId ON dbo.RealtimeHubLog (UserId, CreationTime DESC);
    CREATE INDEX IX_RealtimeHubLog_SiteId ON dbo.RealtimeHubLog (SiteId, CreationTime DESC);
END
GO

IF OBJECT_ID(N'dbo.RealtimeEventLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RealtimeEventLog (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        HubName NVARCHAR(64) NOT NULL,
        Feature NVARCHAR(64) NOT NULL,
        EventName NVARCHAR(64) NOT NULL,
        SiteId INT NULL,
        AccountId INT NULL,
        EntityType NVARCHAR(32) NULL,
        EntityId NVARCHAR(64) NULL,
        PayloadJson NVARCHAR(MAX) NULL,
        Success BIT NOT NULL CONSTRAINT DF_RealtimeEventLog_Success DEFAULT (1),
        Detail NVARCHAR(500) NULL,
        CreationTime DATETIME2(0) NOT NULL CONSTRAINT DF_RealtimeEventLog_CreationTime DEFAULT (SYSUTCDATETIME())
    );
    CREATE INDEX IX_RealtimeEventLog_Hub_Feature ON dbo.RealtimeEventLog (HubName, Feature, CreationTime DESC);
    CREATE INDEX IX_RealtimeEventLog_SiteId ON dbo.RealtimeEventLog (SiteId, CreationTime DESC);
    CREATE INDEX IX_RealtimeEventLog_Entity ON dbo.RealtimeEventLog (EntityType, EntityId, CreationTime DESC);
    CREATE INDEX IX_RealtimeEventLog_CreationTime ON dbo.RealtimeEventLog (CreationTime DESC);
END
GO
