-- Meshek Basar PT (site 48, account 44) - three PT posts are owned by the wrong George product.
-- The 2026-09-01 repair chose owners by slug; the LIVE posts (name + SKU = what customers buy) say otherwise:
--   post 4047 'עוף טחון'        sku 123468 -> is 12718, but owned by 12761 (אסאדו עם עצם, qty 0 -> post shows "אזל")
--   post 4056 'חזה עוף חצוי'    sku 454577 -> is 12719, but owned by 12771 (המבורגר אמריקאי)
--   post 4062 'צלי כתף מס׳ 5'   sku 123478 -> is 12699, but owned by 12774 (קבב מזרחי);
--        12699 meanwhile got a site-48 SKU 2123478 and its own duplicate PT post 4532 ('צלי כתף מס׳ 5-2', 2026-09-02)
-- Consequences: site orders for these posts link to the wrong George product (picking), the real products
-- cannot sync (legacy ids 3727/3733/3743 are ROOT-install ids; create is rejected "מק"ט לא תקף או כפול"),
-- and the wrong owners' stock pushes drive the live posts' stock status.
-- 12761 / 12771 / 12774 have NO post of their own on PT - after this script, saving each in George creates one.
--
-- Afterwards, in George: save 12718, 12719, 12699 (sync -> live posts get the right stock),
-- then save 12761, 12771, 12774 (sync -> new PT posts). In Woo PT: trash duplicate post 4532 and
-- optionally fix the slugs of 4047 (אסאדו-עם-עצם) - sync never rewrites an existing post's slug.
--
-- Run block 1 (preview), then block 2 inside the transaction, check the counts, then COMMIT manually.

-- ===== 1. Preview =====
SELECT 'claim' AS what, Id, ProductId, SiteId, WooCommerceProductId
FROM dbo.ProductSiteWooId
WHERE SiteId = 48 AND (WooCommerceProductId IN (4047, 4056, 4062, 4532) OR ProductId IN (12718, 12719, 12699, 12761, 12771, 12774))
ORDER BY WooCommerceProductId;

SELECT 'override' AS what, ProductId, SiteId, Sku, Name
FROM dbo.ProductSiteOverride WHERE ProductId = 12699 AND SiteId = 48 AND IsDeleted = 0;

-- ===== 2. Apply (COMMIT manually after checking the counts) =====
BEGIN TRAN;

-- Wrong owners lose their PT claims (they get fresh posts on the next save).
DELETE FROM dbo.ProductSiteWooId
WHERE SiteId = 48 AND (
       (ProductId = 12761 AND WooCommerceProductId = 4047)
    OR (ProductId = 12771 AND WooCommerceProductId = 4056)
    OR (ProductId = 12774 AND WooCommerceProductId = 4062)
    OR (ProductId = 12699 AND WooCommerceProductId = 4532));   -- 12699's duplicate post
SELECT @@ROWCOUNT AS deleted_wrong_claims;   -- expect 4

-- Rightful owners claim the live posts (unique index on (SiteId, WooCommerceProductId) is now free).
INSERT INTO dbo.ProductSiteWooId (ProductId, SiteId, WooCommerceProductId)
SELECT v.ProductId, 48, v.WooId
FROM (VALUES (12718, 4047), (12719, 4056), (12699, 4062)) AS v(ProductId, WooId)
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductSiteWooId m WHERE m.ProductId = v.ProductId AND m.SiteId = 48);
SELECT @@ROWCOUNT AS inserted_right_claims;  -- expect 3

-- 12699 on site 48: back to the real SKU (the 2123478 workaround belonged to the duplicate post).
UPDATE dbo.ProductSiteOverride SET Sku = N'123478', UpdatedDate = SYSUTCDATETIME()
WHERE ProductId = 12699 AND SiteId = 48 AND IsDeleted = 0 AND Sku = N'2123478';
SELECT @@ROWCOUNT AS restored_12699_sku;    -- expect 1

-- Verify: each live post has exactly one site-48 claim, by the right product.
SELECT Id, ProductId, SiteId, WooCommerceProductId
FROM dbo.ProductSiteWooId WHERE SiteId = 48 AND WooCommerceProductId IN (4047, 4056, 4062, 4532)
ORDER BY WooCommerceProductId;

-- ROLLBACK;
-- COMMIT;
