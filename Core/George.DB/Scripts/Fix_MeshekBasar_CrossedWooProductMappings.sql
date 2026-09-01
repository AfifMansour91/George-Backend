-- Meshek Basar (account 44, sites 47 = meshek-basar.co.il / 48 = .../pt/): four products carry a
-- ProductSiteWooId row on site 48 that points at ANOTHER product's WooCommerce id, so storefront
-- orders resolve to the wrong catalog product (customer report 2026-09-01: "קבב טורקי יורד כסטייק
-- פרגית") and picking scans then find no matching order line. Verified against the live store via
-- wc/v3 (names + SKUs) and against orders 6201/7454 (LineSku 2987452 = קבב טורקי, resolved to 12708).
--
-- The crossed pairs (wrong claimant / rightful owner, per live-store name+SKU):
--   Woo 4047 אסאדו עם עצם  (sku 123483): wrong 12718 עוף טחון      | right 12761
--   Woo 4056 המבורגר אמריקאי (sku 654717): wrong 12719 חזה עוף חצוי | right 12771
--   Woo 4060 קבב טורקי | קפוא          : wrong 12708 סטייק פרגית  | right 12773
--   Woo 4062 קבב מזרחי | קפוא          : wrong 12699 צלי כתף מס 5 | right 12774
-- Each wrong claimant keeps its own correct site-47 row (12718→3727, 12719→3733, 12708→3738,
-- 12699→3743); its legacy Product.WooCommerceId also holds the crossed id and is repointed to the
-- site-47 id. 12773 additionally gets Sku 2987452 (scale PLU printed on the product's label; the
-- picking scanner matches on it and the column is currently NULL).
--
-- Run against George.Prod. Review the previews, then COMMIT manually at the end.

-- ============ PREVIEW ============
SELECT 'map rows to delete' AS What, w.ProductId, w.SiteId, w.WooCommerceProductId
FROM ProductSiteWooId w
WHERE (w.ProductId = 12718 AND w.SiteId = 48 AND w.WooCommerceProductId = 4047)
   OR (w.ProductId = 12719 AND w.SiteId = 48 AND w.WooCommerceProductId = 4056)
   OR (w.ProductId = 12708 AND w.SiteId = 48 AND w.WooCommerceProductId = 4060)
   OR (w.ProductId = 12699 AND w.SiteId = 48 AND w.WooCommerceProductId = 4062);

SELECT 'legacy before' AS What, Id, Name, Sku, WooCommerceId
FROM Product WHERE Id IN (12718, 12719, 12708, 12699, 12773);

-- ============ APPLY ============
BEGIN TRAN;

DELETE FROM ProductSiteWooId
WHERE (ProductId = 12718 AND SiteId = 48 AND WooCommerceProductId = 4047)
   OR (ProductId = 12719 AND SiteId = 48 AND WooCommerceProductId = 4056)
   OR (ProductId = 12708 AND SiteId = 48 AND WooCommerceProductId = 4060)
   OR (ProductId = 12699 AND SiteId = 48 AND WooCommerceProductId = 4062);
SELECT @@ROWCOUNT AS DeletedMapRows; -- expect 4

UPDATE Product SET WooCommerceId = 3727 WHERE Id = 12718 AND WooCommerceId = 4047;
UPDATE Product SET WooCommerceId = 3733 WHERE Id = 12719 AND WooCommerceId = 4056;
UPDATE Product SET WooCommerceId = 3738 WHERE Id = 12708 AND WooCommerceId = 4060;
UPDATE Product SET WooCommerceId = 3743 WHERE Id = 12699 AND WooCommerceId = 4062;

-- Scale PLU for קבב טורקי | קפוא (label barcode 2-987452-00698-0 → PLU 2987452).
UPDATE Product SET Sku = N'2987452' WHERE Id = 12773 AND (Sku IS NULL OR LTRIM(RTRIM(Sku)) = N'');

-- ============ VERIFY (inside the tran) ============
-- Each Woo id must now have exactly ONE active claimant on site 48: 4047→12761, 4056→12771, 4060→12773, 4062→12774.
SELECT w.WooCommerceProductId, w.ProductId, p.Name, p.Sku
FROM ProductSiteWooId w JOIN Product p ON p.Id = w.ProductId AND p.IsDeleted = 0
WHERE w.SiteId = 48 AND w.WooCommerceProductId IN (4047, 4056, 4060, 4062)
ORDER BY w.WooCommerceProductId;

SELECT 'legacy after' AS What, Id, Name, Sku, WooCommerceId
FROM Product WHERE Id IN (12718, 12719, 12708, 12699, 12773);

-- COMMIT;   -- run after reviewing the verify output
-- ROLLBACK; -- if anything looks off
