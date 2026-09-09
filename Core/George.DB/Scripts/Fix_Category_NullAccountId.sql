-- Categories saved with AccountId = NULL are invisible to every account-scoped read (the category list
-- filters c.AccountId == account; only system admins, who skip that filter, see them) and are skipped
-- by per-site Woo sync. Hinnawi Jaffa (account 13) 2026-09-09: their "מזווה" (category 884, 3 sub-
-- categories, 167 products on site 13) was created 2026-04-28 by an admin user and never got an account,
-- so the shop could not see it anywhere (products, iPad, new order) while the super user could.
-- The code guard (CategoryService.EnsureAccountIdAsync) stops NEW rows; this repairs the legacy ones.
--
-- Prod audit 2026-09-09: 26 active null-account rows.
--   * 884 מזווה is a genuine category (site 13; account 13's other pantry 626 lives on site 14) -> just set AccountId.
--   * 19 rows are DUPLICATES of an active category the account already has (same name, same parent, same
--     sites, mostly the same Woo id - both halves of one import). Giving them an AccountId would show the
--     shop two identical categories, so they are MERGED into the visible twin instead: products, per-site
--     product categories, media, site links, Woo id and children move over; the null row is soft-deleted.
--     Some hidden twins hold the real products (709 פסטות ואורז: 17 active products vs none active in the
--     visible 632; 809 סלטים: 54 vs 2) - those products become visible under the category the shop knows.
--   * 6 rows (209-214) have no sites, products or children - empty leftovers, left untouched.
--
-- Run block 1 (preview), then block 2 inside the transaction, check the counts, then COMMIT manually.

-- ===== 1. Preview =====
DECLARE @pairs TABLE (NullId INT PRIMARY KEY, KeepId INT NOT NULL, AccountId INT NOT NULL);
INSERT @pairs VALUES
    (99, 98, 6), (112, 104, 6), (123, 122, 12), (146, 145, 7), (178, 177, 14), (197, 363, 15), (201, 200, 15),
    (707, 692, 20), (709, 632, 13), (713, 920, 22), (714, 927, 22), (743, 742, 18), (770, 923, 22), (771, 924, 22),
    (772, 922, 22), (773, 921, 22), (774, 925, 22), (775, 926, 22), (809, 1348, 5);

-- Sanity: every pair is (null-account active row) -> (active row of that account, same name, same parent).
SELECT p.NullId, n.Name AS NullName, n.WooCommerceId AS NullWoo, p.KeepId, k.Name AS KeepName, k.AccountId AS KeepAccount, k.WooCommerceId AS KeepWoo,
    CASE WHEN n.AccountId IS NULL AND n.IsDeleted = 0 AND k.IsDeleted = 0 AND k.AccountId = p.AccountId
              AND LTRIM(RTRIM(n.Name)) = LTRIM(RTRIM(k.Name)) AND ISNULL(n.ParentCategoryId, 0) = ISNULL(k.ParentCategoryId, 0)
         THEN 'ok' ELSE 'MISMATCH - STOP' END AS Check_,
    (SELECT COUNT(*) FROM dbo.ProductCategory pc WHERE pc.CategoryId = p.NullId) AS NullProducts,
    (SELECT COUNT(*) FROM dbo.ProductCategory pc WHERE pc.CategoryId = p.KeepId) AS KeepProducts,
    (SELECT COUNT(*) FROM dbo.Category c WHERE c.ParentCategoryId = p.NullId) AS NullChildren
FROM @pairs p JOIN dbo.Category n ON n.Id = p.NullId JOIN dbo.Category k ON k.Id = p.KeepId
ORDER BY p.NullId;

-- Null-account rows NOT covered by the pairs (expect exactly 884 + the six empty leftovers 209-214).
SELECT c.Id, c.Name, c.ParentCategoryId,
    (SELECT COUNT(*) FROM dbo.CategorySite cs WHERE cs.CategoryId = c.Id) AS Sites,
    (SELECT COUNT(*) FROM dbo.ProductCategory pc WHERE pc.CategoryId = c.Id) AS Products,
    (SELECT COUNT(*) FROM dbo.Category k WHERE k.ParentCategoryId = c.Id AND k.IsDeleted = 0) AS Children
FROM dbo.Category c
WHERE c.AccountId IS NULL AND c.IsDeleted = 0 AND c.Id NOT IN (SELECT NullId FROM @pairs)
ORDER BY c.Id;

-- ===== 2. Apply (COMMIT manually after checking the counts) =====
BEGIN TRAN;

DECLARE @m TABLE (NullId INT PRIMARY KEY, KeepId INT NOT NULL, AccountId INT NOT NULL);
INSERT @m VALUES
    (99, 98, 6), (112, 104, 6), (123, 122, 12), (146, 145, 7), (178, 177, 14), (197, 363, 15), (201, 200, 15),
    (707, 692, 20), (709, 632, 13), (713, 920, 22), (714, 927, 22), (743, 742, 18), (770, 923, 22), (771, 924, 22),
    (772, 922, 22), (773, 921, 22), (774, 925, 22), (775, 926, 22), (809, 1348, 5);

-- Abort if any pair no longer matches the audit (someone edited a category in the meantime).
IF EXISTS (
    SELECT 1 FROM @m p JOIN dbo.Category n ON n.Id = p.NullId JOIN dbo.Category k ON k.Id = p.KeepId
    WHERE NOT (n.AccountId IS NULL AND n.IsDeleted = 0 AND k.IsDeleted = 0 AND k.AccountId = p.AccountId
               AND LTRIM(RTRIM(n.Name)) = LTRIM(RTRIM(k.Name)) AND ISNULL(n.ParentCategoryId, 0) = ISNULL(k.ParentCategoryId, 0)))
BEGIN
    RAISERROR('Pair sanity check failed - re-run the preview and fix the pairs before applying.', 16, 1);
    ROLLBACK; RETURN;
END

-- 2a. Products: add the product to the kept category where missing (keep IsPrimary), then drop the old link.
INSERT INTO dbo.ProductCategory (ProductId, CategoryId, IsPrimary)
SELECT pc.ProductId, p.KeepId, pc.IsPrimary
FROM dbo.ProductCategory pc JOIN @m p ON p.NullId = pc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategory x WHERE x.ProductId = pc.ProductId AND x.CategoryId = p.KeepId);
SELECT @@ROWCOUNT AS products_moved;            -- expect 78 (2+17+2+1+1+1+54) minus any already in both

DELETE pc FROM dbo.ProductCategory pc JOIN @m p ON p.NullId = pc.CategoryId;
SELECT @@ROWCOUNT AS product_links_removed;     -- expect 78

-- 2b. Per-site product categories (unique on ProductId, SiteId, CategoryId).
UPDATE psc SET psc.CategoryId = p.KeepId
FROM dbo.ProductSiteCategory psc JOIN @m p ON p.NullId = psc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductSiteCategory x WHERE x.ProductId = psc.ProductId AND x.SiteId = psc.SiteId AND x.CategoryId = p.KeepId);
DELETE psc FROM dbo.ProductSiteCategory psc JOIN @m p ON p.NullId = psc.CategoryId;
SELECT @@ROWCOUNT AS site_product_links_removed; -- expect 0 (none existed in the audit)

-- 2c. Media links.
UPDATE mc SET mc.CategoryId = p.KeepId
FROM dbo.MediaCategory mc JOIN @m p ON p.NullId = mc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.MediaCategory x WHERE x.MediaId = mc.MediaId AND x.CategoryId = p.KeepId);
DELETE mc FROM dbo.MediaCategory mc JOIN @m p ON p.NullId = mc.CategoryId;

-- 2d. Site links: the kept category gets every site the hidden twin had, then the twin's rows go.
INSERT INTO dbo.CategorySite (CategoryId, SiteId)
SELECT p.KeepId, cs.SiteId
FROM dbo.CategorySite cs JOIN @m p ON p.NullId = cs.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.CategorySite x WHERE x.CategoryId = p.KeepId AND x.SiteId = cs.SiteId);
SELECT @@ROWCOUNT AS site_links_added;          -- expect 0 (same sites in the audit)
DELETE cs FROM dbo.CategorySite cs JOIN @m p ON p.NullId = cs.CategoryId;
SELECT @@ROWCOUNT AS site_links_removed;        -- expect 19

-- 2e. Woo ids: per-site map rows move where the kept row has none for that site; legacy column filled when empty.
UPDATE w SET w.CategoryId = p.KeepId
FROM dbo.CategorySiteWooId w JOIN @m p ON p.NullId = w.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.CategorySiteWooId x WHERE x.CategoryId = p.KeepId AND x.SiteId = w.SiteId);
DELETE w FROM dbo.CategorySiteWooId w JOIN @m p ON p.NullId = w.CategoryId;
UPDATE k SET k.WooCommerceId = n.WooCommerceId
FROM dbo.Category k JOIN @m p ON p.KeepId = k.Id JOIN dbo.Category n ON n.Id = p.NullId
WHERE k.WooCommerceId IS NULL AND n.WooCommerceId IS NOT NULL;
SELECT @@ROWCOUNT AS woo_ids_filled;            -- expect 1 (1348 סלטים <- 587)

-- 2f. Children re-parented (none in the audit; kept for safety).
UPDATE c SET c.ParentCategoryId = p.KeepId FROM dbo.Category c JOIN @m p ON p.NullId = c.ParentCategoryId;

-- 2g. Retire the hidden twins (stamped with the account so nothing stays NULL).
UPDATE c SET c.IsDeleted = 1, c.AccountId = p.AccountId, c.UpdatedDate = SYSUTCDATETIME()
FROM dbo.Category c JOIN @m p ON p.NullId = c.Id;
SELECT @@ROWCOUNT AS twins_retired;             -- expect 19

-- 2h. Hinnawi Jaffa's pantry: a real category, just missing its account.
UPDATE dbo.Category SET AccountId = 13, UpdatedDate = SYSUTCDATETIME() WHERE Id = 884 AND AccountId IS NULL;
SELECT @@ROWCOUNT AS pantry_fixed;              -- expect 1

-- Verify: only the six empty leftovers remain without an account; no same-name duplicates inside any account.
SELECT Id, Name FROM dbo.Category WHERE AccountId IS NULL AND IsDeleted = 0 ORDER BY Id;   -- expect 209..214
SELECT AccountId, LTRIM(RTRIM(Name)) AS Name, ISNULL(ParentCategoryId, 0) AS Parent, COUNT(*) AS cnt
FROM dbo.Category WHERE IsDeleted = 0 AND AccountId IS NOT NULL
GROUP BY AccountId, LTRIM(RTRIM(Name)), ISNULL(ParentCategoryId, 0) HAVING COUNT(*) > 1;   -- expect no rows
SELECT Id, Name, AccountId FROM dbo.Category WHERE Id IN (884, 626);

-- ROLLBACK;
-- COMMIT;
