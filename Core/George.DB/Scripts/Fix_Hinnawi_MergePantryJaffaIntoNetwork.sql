-- Hinnawi (account 13): make ONE "מזווה" serve both stores.
-- Category 884 "מזווה" (site 13 Jaffa, Woo 784, no AccountId) cannot simply get AccountId = 13: the unique
-- index UX_Category_Account_Parent_Name_NotDeleted already holds (13, root, "מזווה") for 626 (site 14 Herzliya).
-- The multi-site model wants one account category linked to both sites with a Woo id PER SITE
-- (CategorySiteWooId), so 884 is merged into 626, and its only same-named child (639 "תבלינים") into
-- 626's child 631 "תבלינים". The other two Jaffa children (637, 638) just move under 626 and stay site-13-only.
-- No product sits in both halves of either pair (checked 2026-09-09), no media / per-site product-category rows.
--
-- Run block 1 (preview), then block 2 inside the transaction, check the counts, then COMMIT manually.
-- Afterwards no deploy is needed: the shop sees "מזווה" on both sites at once (reload the page).

-- ===== 1. Preview =====
SELECT c.Id, c.Name, c.ParentCategoryId, c.AccountId, c.IsDeleted, c.WooCommerceId,
    (SELECT STRING_AGG(CAST(cs.SiteId AS varchar), ',') FROM dbo.CategorySite cs WHERE cs.CategoryId = c.Id) AS Sites,
    (SELECT STRING_AGG(CAST(w.SiteId AS varchar) + '->' + CAST(w.WooCommerceCategoryId AS varchar), ',') FROM dbo.CategorySiteWooId w WHERE w.CategoryId = c.Id) AS WooPerSite,
    (SELECT COUNT(*) FROM dbo.ProductCategory pc WHERE pc.CategoryId = c.Id) AS Products
FROM dbo.Category c WHERE c.Id IN (626, 884, 631, 639, 637, 638)
ORDER BY c.ParentCategoryId, c.Id;
-- expect: 626 (13, site 14, 14->35, 349) / 884 (NULL, site 13, 13->784, 153) / 631 (site 14, 14->44, 105) /
--         639 (site 13, 13->790, 35) / 637 (site 13, 13->789, 106) / 638 (site 13, 13->787, 30)

SELECT 'overlap 884&626' AS what, COUNT(*) AS cnt FROM dbo.ProductCategory a JOIN dbo.ProductCategory b ON b.ProductId = a.ProductId AND b.CategoryId = 626 WHERE a.CategoryId = 884
UNION ALL SELECT 'overlap 639&631', COUNT(*) FROM dbo.ProductCategory a JOIN dbo.ProductCategory b ON b.ProductId = a.ProductId AND b.CategoryId = 631 WHERE a.CategoryId = 639;
-- expect 0 / 0

-- ===== 2. Apply (COMMIT manually after checking the counts) =====
BEGIN TRAN;

DECLARE @m TABLE (FromId INT PRIMARY KEY, ToId INT NOT NULL);
INSERT @m VALUES (884, 626), (639, 631);

-- Guard: both sources still as audited (unaccounted/active pantry, active תבלינים under it), targets active on account 13.
IF NOT EXISTS (SELECT 1 FROM dbo.Category WHERE Id = 884 AND AccountId IS NULL AND IsDeleted = 0 AND ParentCategoryId IS NULL)
   OR NOT EXISTS (SELECT 1 FROM dbo.Category WHERE Id = 639 AND ParentCategoryId = 884 AND IsDeleted = 0)
   OR NOT EXISTS (SELECT 1 FROM dbo.Category WHERE Id = 626 AND AccountId = 13 AND IsDeleted = 0 AND ParentCategoryId IS NULL)
   OR NOT EXISTS (SELECT 1 FROM dbo.Category WHERE Id = 631 AND ParentCategoryId = 626 AND AccountId = 13 AND IsDeleted = 0)
BEGIN
    RAISERROR('Categories changed since the audit - re-check before applying.', 16, 1);
    ROLLBACK; RETURN;
END

-- 2a. Products -> target (keep IsPrimary), old links removed.
INSERT INTO dbo.ProductCategory (ProductId, CategoryId, IsPrimary)
SELECT pc.ProductId, m.ToId, pc.IsPrimary
FROM dbo.ProductCategory pc JOIN @m m ON m.FromId = pc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategory x WHERE x.ProductId = pc.ProductId AND x.CategoryId = m.ToId);
SELECT @@ROWCOUNT AS products_moved;                 -- expect 188 (153 + 35)
DELETE pc FROM dbo.ProductCategory pc JOIN @m m ON m.FromId = pc.CategoryId;
SELECT @@ROWCOUNT AS product_links_removed;          -- expect 188

-- 2b. Per-site product categories / media (none in the audit; safe no-ops).
UPDATE psc SET psc.CategoryId = m.ToId FROM dbo.ProductSiteCategory psc JOIN @m m ON m.FromId = psc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductSiteCategory x WHERE x.ProductId = psc.ProductId AND x.SiteId = psc.SiteId AND x.CategoryId = m.ToId);
DELETE psc FROM dbo.ProductSiteCategory psc JOIN @m m ON m.FromId = psc.CategoryId;
UPDATE mc SET mc.CategoryId = m.ToId FROM dbo.MediaCategory mc JOIN @m m ON m.FromId = mc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.MediaCategory x WHERE x.MediaId = mc.MediaId AND x.CategoryId = m.ToId);
DELETE mc FROM dbo.MediaCategory mc JOIN @m m ON m.FromId = mc.CategoryId;

-- 2c. Targets now live on site 13 as well.
INSERT INTO dbo.CategorySite (CategoryId, SiteId)
SELECT m.ToId, cs.SiteId FROM dbo.CategorySite cs JOIN @m m ON m.FromId = cs.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.CategorySite x WHERE x.CategoryId = m.ToId AND x.SiteId = cs.SiteId);
SELECT @@ROWCOUNT AS site_links_added;               -- expect 2 (626->13, 631->13)
DELETE cs FROM dbo.CategorySite cs JOIN @m m ON m.FromId = cs.CategoryId;

-- 2d. Site-13 Woo ids move to the targets (626: 13->784, 631: 13->790); site-14 ids stay as they are.
UPDATE w SET w.CategoryId = m.ToId FROM dbo.CategorySiteWooId w JOIN @m m ON m.FromId = w.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.CategorySiteWooId x WHERE x.CategoryId = m.ToId AND x.SiteId = w.SiteId);
SELECT @@ROWCOUNT AS woo_ids_moved;                  -- expect 2
DELETE w FROM dbo.CategorySiteWooId w JOIN @m m ON m.FromId = w.CategoryId;

-- 2e. Remaining Jaffa children (637 רטבים- חומץ- שמן, 638 פסטות) re-parented under the shared pantry.
UPDATE dbo.Category SET ParentCategoryId = 626, UpdatedDate = SYSUTCDATETIME()
WHERE ParentCategoryId = 884 AND Id <> 639;
SELECT @@ROWCOUNT AS children_reparented;            -- expect 2

-- 2f. Retire the merged sources (AccountId stamped so nothing stays NULL).
UPDATE c SET c.IsDeleted = 1, c.AccountId = 13, c.UpdatedDate = SYSUTCDATETIME()
FROM dbo.Category c JOIN @m m ON m.FromId = c.Id;
SELECT @@ROWCOUNT AS sources_retired;                -- expect 2

-- Verify: pantry tree on both sites, per-site Woo ids intact, nothing left under 884.
SELECT c.Id, c.Name, c.ParentCategoryId, c.AccountId, c.IsDeleted,
    (SELECT STRING_AGG(CAST(cs.SiteId AS varchar), ',') FROM dbo.CategorySite cs WHERE cs.CategoryId = c.Id) AS Sites,
    (SELECT STRING_AGG(CAST(w.SiteId AS varchar) + '->' + CAST(w.WooCommerceCategoryId AS varchar), ',') FROM dbo.CategorySiteWooId w WHERE w.CategoryId = c.Id) AS WooPerSite,
    (SELECT COUNT(*) FROM dbo.ProductCategory pc WHERE pc.CategoryId = c.Id) AS Products
FROM dbo.Category c WHERE c.Id = 626 OR c.ParentCategoryId = 626 OR c.Id IN (884, 639) OR c.ParentCategoryId = 884
ORDER BY c.ParentCategoryId, c.Id;
-- expect: 626 sites 13,14 / 13->784,14->35 / 502 products; 631 sites 13,14 / 13->790,14->44 / 140; 637+638 under 626 on site 13;
--         884 and 639 IsDeleted=1 with 0 products; nothing else under 884.

-- ROLLBACK;
-- COMMIT;
