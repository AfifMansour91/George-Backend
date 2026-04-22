-- Promotions (Sprint 4): run against the George DB.
IF OBJECT_ID(N'dbo.Promotion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Promotion (
        Id                  INT            IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
        IsDeleted           BIT            NOT NULL CONSTRAINT DF_Promotion_IsDeleted DEFAULT (0),
        GuidId              UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Promotion_GuidId DEFAULT (NEWID()),
        CreationTime        DATETIME2(0)   NOT NULL CONSTRAINT DF_Promotion_CreationTime DEFAULT (SYSUTCDATETIME()),
        UpdatedDate         DATETIME2(0)   NULL,
        CreationUserId      INT            NULL,
        UpdateUserId        INT            NULL,
        SiteId              INT            NOT NULL,
        PromotionType       NVARCHAR(40)   NOT NULL,
        Name                NVARCHAR(500)  NOT NULL,
        IsActive            BIT            NOT NULL CONSTRAINT DF_Promotion_IsActive DEFAULT (1),
        ShowBadge           BIT            NOT NULL CONSTRAINT DF_Promotion_ShowBadge DEFAULT (0),
        IsDraft             BIT            NOT NULL CONSTRAINT DF_Promotion_IsDraft DEFAULT (1),
        ScheduleStartDateUtc DATETIME2(0)  NULL,
        ScheduleEndDateUtc   DATETIME2(0)  NULL,
        PayloadJson         NVARCHAR(MAX)  NOT NULL CONSTRAINT DF_Promotion_PayloadJson DEFAULT ('{}'),
        CONSTRAINT FK_Promotion_Site FOREIGN KEY (SiteId) REFERENCES dbo.Site (Id)
    );

    CREATE NONCLUSTERED INDEX IX_Promotion_SiteId_IsDeleted
        ON dbo.Promotion (SiteId, IsDeleted)
        WHERE IsDeleted = 0;
END
GO
