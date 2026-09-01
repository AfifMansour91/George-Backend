-- Meatology (account 23, site 25 = meatbrain.co.il): Woo product 2920 is claimed by BOTH
-- George 5379 (בקר טחון פרמיום) and 11419 (בשר טחון 2 ק"ג ב-99), which share Sku 2244028.
-- Storefront orders 3113 + 4249 prove Woo 2920 is the 99₪ promo product (line title + 99₪ price),
-- yet both resolved to 5379 - the older claim wins. Rightful owner: 11419.
-- Fix: drop 5379's claim (map row + legacy column). 5379's true Woo id is unknown (store was
-- unreachable during the check); its next sync will refuse the taken id (ownership guard) and
-- create/link properly. NOTE the shared Sku 2244028 on two George products is itself a data smell
-- the account should clean up - the sync guard will log it on 5379's next save.
--
-- Run against George.Prod AFTER the ProductSiteWooId cleanup is committed, BEFORE creating the
-- unique indexes. Review previews, then COMMIT manually.

-- ============ PREVIEW ============
SELECT w.Id AS RowId, w.ProductId, w.SiteId, w.WooCommerceProductId
FROM ProductSiteWooId w WHERE w.SiteId = 25 AND w.WooCommerceProductId = 2920;  -- expect rows 3132 (5379) + 6591 (11419)

SELECT Id, Name, Sku, WooCommerceId FROM Product WHERE Id IN (5379, 11419);

-- ============ APPLY ============
BEGIN TRAN;

DELETE FROM ProductSiteWooId WHERE ProductId = 5379 AND SiteId = 25 AND WooCommerceProductId = 2920;
SELECT @@ROWCOUNT AS DeletedRows; -- expect 1

UPDATE Product SET WooCommerceId = NULL WHERE Id = 5379 AND WooCommerceId = 2920;
SELECT @@ROWCOUNT AS LegacyCleared; -- expect 1

-- Verify: exactly one claimant left for (25, 2920) and it is 11419.
SELECT w.ProductId, p.Name, p.Sku
FROM ProductSiteWooId w JOIN Product p ON p.Id = w.ProductId
WHERE w.SiteId = 25 AND w.WooCommerceProductId = 2920;

-- COMMIT;   -- run after reviewing
-- ROLLBACK; -- if anything looks off
