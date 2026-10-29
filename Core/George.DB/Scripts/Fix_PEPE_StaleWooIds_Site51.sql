-- PEPE (account 46, site 51) - 2026-10-28
-- The store pepe.deliz.co.il was rebuilt on 2026-10-26 (every product recreated 10:25-10:57, post-id counter
-- restarted). 695 of 735 Giorgio products were re-created / adopted on that run; 39 products kept their OLD
-- Woo ids (4992-5094) which in the new store are VARIATION posts of other products (verified via REST:
-- 39 variation, 1 gone, 0 real product). PUT /products/{variation id} crashes WordPress ("שגיאה קריטית"),
-- so these 39 fail on every sync and never get created.
--
-- This script clears the stale ids so the next sync CREATES the products (the create path with duplicate-SKU
-- adoption exists in the current prod build too - no deploy needed). The variation ids of these products are
-- old-store ids as well (136 legacy ids, 0 per-site rows) and are cleared so the new variations start clean.
--
-- 13497 (SKU N441, "נתחי טונה צהובה -פחית ORTIZ") is NOT touched: it is a Giorgio-side duplicate of 12986,
-- which already owns Woo post 3416 with that SKU. Delete or re-SKU 13497 in Giorgio instead.
--
-- Run block 1 (preview). Then block 2 inside the transaction and COMMIT only if the counts match the preview.
SET NOCOUNT ON;

DECLARE @ids TABLE (ProductId INT PRIMARY KEY);
INSERT INTO @ids (ProductId) VALUES
(13437),(13438),(13439),(13440),(13441),(13442),(13443),(13444),(13445),(13446),
(13448),(13449),(13450),(13451),(13452),(13453),(13454),(13455),(13456),(13457),
(13458),(13462),(13463),(13464),(13465),(13466),(13468),(13469),(13470),(13471),
(13472),(13473),(13474),(13475),(13477),(13478),(13479),(13482),(13483);

-- Guard: only products whose latest site-51 sync is still a failure (re-running after a successful sync is a no-op).
DELETE i FROM @ids i
WHERE NOT EXISTS (SELECT 1 FROM ProductSiteWooSyncStatus st WHERE st.ProductId = i.ProductId AND st.SiteId = 51 AND st.Success = 0);

-------------------------------------------------------------------------------
-- 1) PREVIEW
-------------------------------------------------------------------------------
SELECT p.Id, p.Sku, p.Name, p.WooCommerceId AS LegacyWooId, psw.WooCommerceProductId AS SiteWooId,
       (SELECT COUNT(*) FROM ProductVariant pv WHERE pv.ProductId = p.Id AND pv.IsDeleted = 0 AND pv.WooCommerceVariationId IS NOT NULL) AS VariantsWithLegacyId
FROM Product p
JOIN @ids i ON i.ProductId = p.Id
LEFT JOIN ProductSiteWooId psw ON psw.ProductId = p.Id AND psw.SiteId = 51
ORDER BY p.Id;

SELECT (SELECT COUNT(*) FROM @ids) AS Products,
       (SELECT COUNT(*) FROM ProductSiteWooId psw JOIN @ids i ON i.ProductId = psw.ProductId WHERE psw.SiteId = 51) AS SiteMapRowsToDelete,
       (SELECT COUNT(*) FROM Product p JOIN @ids i ON i.ProductId = p.Id WHERE p.WooCommerceId IS NOT NULL) AS LegacyIdsToClear,
       (SELECT COUNT(*) FROM ProductVariant pv JOIN @ids i ON i.ProductId = pv.ProductId WHERE pv.IsDeleted = 0 AND pv.WooCommerceVariationId IS NOT NULL) AS VariantIdsToClear,
       (SELECT COUNT(*) FROM ProductSiteVariantWooId sv JOIN ProductVariant pv ON pv.Id = sv.ProductVariantId JOIN @ids i ON i.ProductId = pv.ProductId WHERE sv.SiteId = 51) AS SiteVariantRowsToDelete;
-- Expected (2026-10-28): 39 products, 31 site map rows, 39 legacy ids, 136 variant ids, 0 site variant rows.

-------------------------------------------------------------------------------
-- 2) APPLY (manual COMMIT)
-------------------------------------------------------------------------------
BEGIN TRAN;

DELETE psw FROM ProductSiteWooId psw JOIN @ids i ON i.ProductId = psw.ProductId WHERE psw.SiteId = 51;
PRINT CONCAT('ProductSiteWooId rows deleted: ', @@ROWCOUNT);

DELETE sv FROM ProductSiteVariantWooId sv
JOIN ProductVariant pv ON pv.Id = sv.ProductVariantId
JOIN @ids i ON i.ProductId = pv.ProductId
WHERE sv.SiteId = 51;
PRINT CONCAT('ProductSiteVariantWooId rows deleted: ', @@ROWCOUNT);

UPDATE pv SET pv.WooCommerceVariationId = NULL
FROM ProductVariant pv JOIN @ids i ON i.ProductId = pv.ProductId
WHERE pv.WooCommerceVariationId IS NOT NULL;
PRINT CONCAT('ProductVariant legacy variation ids cleared: ', @@ROWCOUNT);

UPDATE p SET p.WooCommerceId = NULL
FROM Product p JOIN @ids i ON i.ProductId = p.Id
WHERE p.WooCommerceId IS NOT NULL;
PRINT CONCAT('Product legacy Woo ids cleared: ', @@ROWCOUNT);

-- Verify, then:
-- COMMIT;
-- ROLLBACK;

-- After COMMIT: in Giorgio open PEPE > catalog and run "סנכרן" for these products (or re-run the CSV import /
-- full sync). Expected result: 39 "created" rows in ProductSiteWooSyncStatus for site 51 and 734 products in the store.
