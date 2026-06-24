-- System-wide integration log: one row per external sync call between George and a store (WooCommerce),
-- across all entity kinds. EntityType (order / product / category / ...) classifies the row so a single
-- admin "sync logs" screen can filter by type / entity / site / date / severity. Stores the full request
-- + response. Idempotent: safe to re-run.

IF OBJECT_ID(N'dbo.IntegrationLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.IntegrationLog
    (
        Id            INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_IntegrationLog PRIMARY KEY,
        SiteId        INT            NOT NULL,
        EntityType    NVARCHAR(30)   NOT NULL,
        EntityId      INT            NULL,
        ExternalId    NVARCHAR(100)  NULL,
        Direction     NVARCHAR(20)   NOT NULL CONSTRAINT DF_IntegrationLog_Direction DEFAULT (N'outbound'),
        Operation     NVARCHAR(80)   NOT NULL,
        [Level]       NVARCHAR(10)   NOT NULL CONSTRAINT DF_IntegrationLog_Level DEFAULT (N'info'),
        Url           NVARCHAR(1000) NULL,
        HttpStatus    INT            NULL,
        Success       BIT            NOT NULL CONSTRAINT DF_IntegrationLog_Success DEFAULT (0),
        RequestJson   NVARCHAR(MAX)  NULL,
        ResponseBody  NVARCHAR(MAX)  NULL,
        DurationMs    INT            NULL,
        Error         NVARCHAR(1000) NULL,
        CreatedAtUtc  DATETIME2(0)   NOT NULL CONSTRAINT DF_IntegrationLog_CreatedAtUtc DEFAULT (sysutcdatetime())
    );
END
GO

-- "logs for site, newest first" (screen's main query).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntegrationLog_Site_Created' AND object_id = OBJECT_ID(N'dbo.IntegrationLog'))
BEGIN
    CREATE INDEX IX_IntegrationLog_Site_Created
        ON dbo.IntegrationLog(SiteId, CreatedAtUtc DESC);
END
GO

-- "logs for site filtered by entity type, newest first".
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntegrationLog_Site_Type_Created' AND object_id = OBJECT_ID(N'dbo.IntegrationLog'))
BEGIN
    CREATE INDEX IX_IntegrationLog_Site_Type_Created
        ON dbo.IntegrationLog(SiteId, EntityType, CreatedAtUtc DESC);
END
GO

-- "logs for a specific entity" (e.g. one order / product / category).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntegrationLog_Entity' AND object_id = OBJECT_ID(N'dbo.IntegrationLog'))
BEGIN
    CREATE INDEX IX_IntegrationLog_Entity
        ON dbo.IntegrationLog(EntityType, EntityId) WHERE EntityId IS NOT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_IntegrationLog_ExternalId' AND object_id = OBJECT_ID(N'dbo.IntegrationLog'))
BEGIN
    CREATE INDEX IX_IntegrationLog_ExternalId
        ON dbo.IntegrationLog(ExternalId) WHERE ExternalId IS NOT NULL;
END
GO
