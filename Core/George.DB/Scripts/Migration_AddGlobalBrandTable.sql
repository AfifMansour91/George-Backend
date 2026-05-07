-- Migration: Add GlobalBrand table
-- Date: 2026-05-07
-- Description: Platform-wide brand catalog managed by super-admins. Mirrors GlobalCategory.
--              Sites/Accounts can "copy down" a GlobalBrand into a local Brand via
--              Brand.SourceGlobalBrandId.
--
-- Idempotent.

------------------------------------------------------------
-- 1. Table
------------------------------------------------------------

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'GlobalBrand' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[GlobalBrand](
        [Id]                     [int] IDENTITY(1,1) NOT NULL,
        [IsDeleted]              [bit] NOT NULL,
        [GuidId]                 [uniqueidentifier] NOT NULL,
        [CreationTime]           [datetime2](0) NOT NULL,
        [UpdatedDate]            [datetime2](0) NULL,
        [CreationUserId]         [int] NULL,
        [UpdateUserId]           [int] NULL,
        [Name]                   [nvarchar](200) NOT NULL,
        [Slug]                   [nvarchar](200) NULL,
        [Description]            [nvarchar](2000) NULL,
        [ParentGlobalBrandId]    [int] NULL,
        [SortOrder]              [int] NULL,
        [ProductCount]           [int] NULL,
        [ImageUrl]               [nvarchar](1000) NULL,
        [IconUrl]                [nvarchar](1000) NULL,
        [SeoTitle]               [nvarchar](200) NULL,
        [SeoDescription]         [nvarchar](500) NULL,
        [WooCommerceBrandId]     [int] NULL,
     CONSTRAINT [PK_GlobalBrand] PRIMARY KEY CLUSTERED
    (
        [Id] ASC
    )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
    ) ON [PRIMARY];

    -- Defaults so EF inserts work without explicit values for these.
    ALTER TABLE [dbo].[GlobalBrand] ADD CONSTRAINT [DF_GlobalBrand_IsDeleted] DEFAULT (0) FOR [IsDeleted];
    ALTER TABLE [dbo].[GlobalBrand] ADD CONSTRAINT [DF_GlobalBrand_GuidId]    DEFAULT (NEWID()) FOR [GuidId];

    PRINT 'Created GlobalBrand table';
END
ELSE
BEGIN
    PRINT 'GlobalBrand table already exists';
END
GO

------------------------------------------------------------
-- 2. Indexes
------------------------------------------------------------

-- Unique brand name across the platform (excluding soft-deleted rows).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GlobalBrand_Name_NotDeleted' AND object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_GlobalBrand_Name_NotDeleted] ON [dbo].[GlobalBrand]
    (
        [Name] ASC
    )
    WHERE ([IsDeleted] = 0)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY];
    PRINT 'Created index UX_GlobalBrand_Name_NotDeleted';
END
GO

-- Unique slug across the platform when set.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_GlobalBrand_Slug_NotDeleted' AND object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [UX_GlobalBrand_Slug_NotDeleted] ON [dbo].[GlobalBrand]
    (
        [Slug] ASC
    )
    WHERE ([IsDeleted] = 0 AND [Slug] IS NOT NULL)
    WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY];
    PRINT 'Created index UX_GlobalBrand_Slug_NotDeleted';
END
GO

-- Hierarchy lookup.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GlobalBrand_ParentGlobalBrandId' AND object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GlobalBrand_ParentGlobalBrandId] ON [dbo].[GlobalBrand]([ParentGlobalBrandId] ASC) ON [PRIMARY];
    PRINT 'Created index IX_GlobalBrand_ParentGlobalBrandId';
END
GO

-- Filtered index on WooCommerceBrandId for sync lookups.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_GlobalBrand_WooCommerceBrandId' AND object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_GlobalBrand_WooCommerceBrandId] ON [dbo].[GlobalBrand]([WooCommerceBrandId] ASC)
        WHERE [WooCommerceBrandId] IS NOT NULL
        ON [PRIMARY];
    PRINT 'Created index IX_GlobalBrand_WooCommerceBrandId';
END
GO

------------------------------------------------------------
-- 3. Foreign keys
------------------------------------------------------------

-- Self-FK: ParentGlobalBrandId -> GlobalBrand.Id
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GlobalBrand_Parent' AND parent_object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    ALTER TABLE [dbo].[GlobalBrand] WITH CHECK ADD CONSTRAINT [FK_GlobalBrand_Parent]
        FOREIGN KEY([ParentGlobalBrandId]) REFERENCES [dbo].[GlobalBrand]([Id]);
    ALTER TABLE [dbo].[GlobalBrand] CHECK CONSTRAINT [FK_GlobalBrand_Parent];
    PRINT 'Added FK_GlobalBrand_Parent';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GlobalBrand_CreationUser' AND parent_object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    ALTER TABLE [dbo].[GlobalBrand] WITH CHECK ADD CONSTRAINT [FK_GlobalBrand_CreationUser]
        FOREIGN KEY([CreationUserId]) REFERENCES [dbo].[User]([Id]);
    ALTER TABLE [dbo].[GlobalBrand] CHECK CONSTRAINT [FK_GlobalBrand_CreationUser];
    PRINT 'Added FK_GlobalBrand_CreationUser';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_GlobalBrand_UpdateUser' AND parent_object_id = OBJECT_ID(N'[dbo].[GlobalBrand]'))
BEGIN
    ALTER TABLE [dbo].[GlobalBrand] WITH CHECK ADD CONSTRAINT [FK_GlobalBrand_UpdateUser]
        FOREIGN KEY([UpdateUserId]) REFERENCES [dbo].[User]([Id]);
    ALTER TABLE [dbo].[GlobalBrand] CHECK CONSTRAINT [FK_GlobalBrand_UpdateUser];
    PRINT 'Added FK_GlobalBrand_UpdateUser';
END
GO

PRINT 'Migration_AddGlobalBrandTable complete.';
