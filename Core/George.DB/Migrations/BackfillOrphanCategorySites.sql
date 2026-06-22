-- Migration: backfill CategorySite links for "orphan" categories.
-- Categories imported / created in "all sites" mode were linked to NO site (empty site list), so they were
-- invisible per-branch in the UI and skipped by the per-site WooCommerce category sync. The code now expands an
-- empty site list to all of the account's sites; this backfills the rows already created broken. Idempotent.
--
-- For every non-deleted category that has an AccountId but ZERO CategorySite rows, link it to every non-deleted
-- site of that account.
INSERT INTO [dbo].[CategorySite] ([CategoryId], [SiteId])
SELECT c.[Id], s.[Id]
FROM [dbo].[Category] c
JOIN [dbo].[Site] s
  ON s.[AccountId] = c.[AccountId]
 AND s.[IsDeleted] = 0
WHERE c.[IsDeleted] = 0
  AND c.[AccountId] IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CategorySite] cs WHERE cs.[CategoryId] = c.[Id])
  AND NOT EXISTS (SELECT 1 FROM [dbo].[CategorySite] x WHERE x.[CategoryId] = c.[Id] AND x.[SiteId] = s.[Id]);
GO
