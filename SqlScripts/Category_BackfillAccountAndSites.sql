-- Backfill Category.AccountId from the site(s) each category is linked to.
--
-- Background: historically Category.AccountId was left NULL on virtually every row. The MultiSite product
-- create/edit screen now fetches categories with GET /Category?Filter.AccountId=<acct> and then keeps, client-side,
-- only those whose site_ids include the selected site (or have no site links at all). A category with the WRONG or
-- NULL AccountId is dropped by the server-side account filter and never reaches the picker — so the branch's
-- categories disappear (e.g. account 54 / site 66).
--
-- A category is owned by the account of the site(s) it lives in. The root cause is fixed in code (CategoryStorage
-- now realigns AccountId to the linked site's account on create/update). This script repairs existing rows.

SET NOCOUNT ON;

-------------------------------------------------------------------------------
-- STEP 1 (SAFE, RECOMMENDED): realign AccountId to the account of the site(s) the category is already linked to.
-- This is authoritative by definition (a category linked to site X belongs to site X's account) and cannot mis-
-- assign. It fixes every linked category, including 1532/1533 (linked to site 66 -> account 54).
-------------------------------------------------------------------------------
UPDATE c
SET c.AccountId = s.AccountId
FROM dbo.Category c
CROSS APPLY (
    SELECT TOP 1 s2.AccountId
    FROM dbo.CategorySite cs
    JOIN dbo.[Site] s2 ON s2.Id = cs.SiteId
    WHERE cs.CategoryId = c.Id
    ORDER BY s2.Id
) s
WHERE c.AccountId IS NULL OR c.AccountId <> s.AccountId;

-------------------------------------------------------------------------------
-- STEP 2 (OPTIONAL — REVIEW BEFORE RUNNING): legacy categories that have NO site link at all.
-- These cannot be resolved from a site, so the account is guessed from the user who created the category. This can
-- mis-assign categories created by master/admin users, so it is left commented out. Inspect the affected rows first:
--
--   SELECT c.Id, c.Name, c.CreationUserId, u.AccountId AS WouldBecome
--   FROM dbo.Category c
--   JOIN dbo.[User] u ON u.Id = c.CreationUserId
--   WHERE c.IsDeleted = 0 AND c.AccountId IS NULL AND u.AccountId IS NOT NULL
--     AND NOT EXISTS (SELECT 1 FROM dbo.CategorySite cs WHERE cs.CategoryId = c.Id);
--
-- Note: a category with no site links is already shown for ALL sites by the picker's client-side filter, so it only
-- needs a correct AccountId to reappear — it does NOT need to be linked to any site.
--
-- UPDATE c
-- SET c.AccountId = u.AccountId
-- FROM dbo.Category c
-- JOIN dbo.[User] u ON u.Id = c.CreationUserId
-- WHERE c.AccountId IS NULL
--   AND u.AccountId IS NOT NULL
--   AND NOT EXISTS (SELECT 1 FROM dbo.CategorySite cs WHERE cs.CategoryId = c.Id);
GO
