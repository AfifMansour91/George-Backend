-- Migration: Add SiteId to AccountMedia so media is scoped per account AND per site.
-- Date: 2026-03-02
-- Description: When an account has multiple sites, each site sees only its own media.
--              Existing rows are backfilled: SiteId = first site (min Id) for that account.
--              Run after Migration_AddAccountMedia.sql. Safe to run multiple times (idempotent where possible).

GO

-- 1. Add SiteId column (nullable first for backfill)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountMedia]') AND name = N'SiteId')
BEGIN
    ALTER TABLE [dbo].[AccountMedia] ADD [SiteId] [int] NULL;
    PRINT 'Added AccountMedia.SiteId (nullable)';
END
GO

-- 2. Backfill: set SiteId to the first (min Id) site for each account
UPDATE am
SET am.[SiteId] = (
    SELECT MIN(s.[Id])
    FROM [dbo].[Site] s
    WHERE s.[AccountId] = am.[AccountId]
      AND s.[IsDeleted] = 0
)
FROM [dbo].[AccountMedia] am
WHERE am.[SiteId] IS NULL;
IF @@ROWCOUNT > 0
    PRINT 'Backfilled AccountMedia.SiteId from first site per account';
GO

-- 3. Remove any rows that could not be backfilled (account has no sites) so we can make SiteId NOT NULL
DELETE am FROM [dbo].[AccountMedia] am WHERE am.[SiteId] IS NULL;
IF @@ROWCOUNT > 0
    PRINT 'Removed AccountMedia rows for accounts with no sites (cannot assign SiteId)';
GO

-- 4. Make SiteId NOT NULL (only if every row has been backfilled)
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[AccountMedia]') AND name = N'SiteId')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM [dbo].[AccountMedia] WHERE [SiteId] IS NULL)
    BEGIN
        ALTER TABLE [dbo].[AccountMedia] ALTER COLUMN [SiteId] [int] NOT NULL;
        PRINT 'AccountMedia.SiteId set to NOT NULL';
    END
    ELSE
        PRINT 'WARNING: Some AccountMedia rows have NULL SiteId (account with no sites). Leaving column nullable.';
END
GO

-- 5. Drop old primary key (AccountId, MediaId)
IF EXISTS (SELECT * FROM sys.key_constraints WHERE name = N'PK_AccountMedia' AND parent_object_id = OBJECT_ID(N'[dbo].[AccountMedia]'))
BEGIN
    ALTER TABLE [dbo].[AccountMedia] DROP CONSTRAINT [PK_AccountMedia];
    PRINT 'Dropped PK_AccountMedia';
END
GO

-- 6. Add new primary key (AccountId, SiteId, MediaId)
IF NOT EXISTS (SELECT * FROM sys.key_constraints WHERE name = N'PK_AccountMedia' AND parent_object_id = OBJECT_ID(N'[dbo].[AccountMedia]'))
BEGIN
    ALTER TABLE [dbo].[AccountMedia] ADD CONSTRAINT [PK_AccountMedia] PRIMARY KEY CLUSTERED ([AccountId] ASC, [SiteId] ASC, [MediaId] ASC);
    PRINT 'Created PK_AccountMedia (AccountId, SiteId, MediaId)';
END
GO

-- 7. Add FK to Site (only if SiteId is NOT NULL for all rows; otherwise skip or use a filtered FK)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = N'FK_AccountMedia_Site')
BEGIN
    ALTER TABLE [dbo].[AccountMedia] WITH CHECK ADD CONSTRAINT [FK_AccountMedia_Site] FOREIGN KEY([SiteId])
    REFERENCES [dbo].[Site] ([Id])
    ON DELETE CASCADE;
    ALTER TABLE [dbo].[AccountMedia] CHECK CONSTRAINT [FK_AccountMedia_Site];
    PRINT 'Added FK_AccountMedia_Site';
END
GO

-- 8. Index for filtering by (AccountId, SiteId)
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = N'IX_AccountMedia_AccountId_SiteId' AND object_id = OBJECT_ID(N'[dbo].[AccountMedia]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccountMedia_AccountId_SiteId] ON [dbo].[AccountMedia]([AccountId] ASC, [SiteId] ASC);
    PRINT 'Created IX_AccountMedia_AccountId_SiteId';
END
GO

PRINT 'Migration_AccountMedia_AddSiteId completed successfully'
GO
