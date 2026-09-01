-- Enforces the ProductSiteWooId invariants that, when broken, cross-link storefront orders to the
-- wrong catalog product (Meshek Basar 1/9: four crossed claims -> kebab orders resolved to pargit):
--   1. One Woo product has at most ONE George owner per site  -> UX_ProductSiteWooId_Site_WooProduct
--   2. One (product, site) has at most ONE map row            -> UX_ProductSiteWooId_Product_Site
-- Duplicate claims used to accumulate silently (mostly stale rows of soft-deleted products left by
-- catalog re-imports); with the indexes a conflicting write fails loudly in the sync log instead.
--
-- Code prerequisites (deploy BEFORE running this): SetSiteWooProductIdAsync self-heals stale claims of
-- deleted products, GetProductIdBySiteWooProductIdAsync ignores deleted owners, and the sync's legacy-id
-- fallback checks ownership. Without that deploy, re-imports would start failing on the unique index.
--
-- Run against George.Prod. Review each preview; COMMIT manually; create the indexes only when both
-- conflict previews return no rows.

-- ============ PREVIEW ============
SELECT 'stale rows of deleted products (will be deleted)' AS What, COUNT(*) AS Cnt
FROM ProductSiteWooId w JOIN Product p ON p.Id = w.ProductId
WHERE p.IsDeleted = 1;

-- ============ APPLY: cleanup ============
BEGIN TRAN;

DELETE w
FROM ProductSiteWooId w JOIN Product p ON p.Id = w.ProductId
WHERE p.IsDeleted = 1;
SELECT @@ROWCOUNT AS DeletedStaleRows;

-- Conflict check 1: same (SiteId, WooCommerceProductId) still claimed by MULTIPLE active products.
-- Must return 0 rows before the indexes can be created; any hits need a per-case decision like the
-- Meshek Basar fix (verify against the live store which product truly owns the Woo id).
SELECT w.SiteId, w.WooCommerceProductId, w.ProductId, p.AccountId, p.Name, p.Sku
FROM ProductSiteWooId w
JOIN Product p ON p.Id = w.ProductId
WHERE EXISTS (
    SELECT 1 FROM ProductSiteWooId w2
    WHERE w2.SiteId = w.SiteId AND w2.WooCommerceProductId = w.WooCommerceProductId AND w2.Id <> w.Id)
ORDER BY w.SiteId, w.WooCommerceProductId, w.ProductId;

-- Conflict check 2: same (ProductId, SiteId) with multiple rows (should not exist; upsert keeps one).
SELECT w.ProductId, w.SiteId, COUNT(*) AS Rows
FROM ProductSiteWooId w
GROUP BY w.ProductId, w.SiteId
HAVING COUNT(*) > 1;

-- COMMIT;   -- after reviewing; if the conflict checks returned rows, resolve them first (new script),
-- ROLLBACK; -- then re-run this script from the top before creating the indexes below.

-- ============ INDEXES (run AFTER the cleanup is committed and both conflict checks are empty) ============
-- IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ProductSiteWooId_Site_WooProduct')
--     CREATE UNIQUE INDEX UX_ProductSiteWooId_Site_WooProduct
--         ON ProductSiteWooId (SiteId, WooCommerceProductId);
-- IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_ProductSiteWooId_Product_Site')
--     CREATE UNIQUE INDEX UX_ProductSiteWooId_Product_Site
--         ON ProductSiteWooId (ProductId, SiteId);
