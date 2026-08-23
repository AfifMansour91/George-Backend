-- Zano Dagim (site 45, account 42) — 23/08/2026 incident: picking charged 190 ₪/kg for לברק instead of 95.
--
-- Root cause chain:
--   1. Woo product sync: PUT /products/{id} failed (site returned 502s on 20/8 ~09:20) and the code fell
--      through to CREATE → DUPLICATE Woo products: lavrak-2 (43933), bar-yam-2 (43932), dennis-2 (44123).
--      ProductSiteWooId now points at the copies; the originals (16169 / 15939 / 15504) are still published
--      and are what customers order. Code fix: create only on 404 (WooCommerceService).
--   2. Order reception: legacy match on Product.WooCommerceId = 16169 is blocked by the collision guard
--      (a ProductSiteWooId row for the same product/site claims a different id) → lines saved with
--      ProductId NULL (13 lines since 20/8 14:03).
--   3. Picking (frontend): unlinked "units" line converted pricePerUnit as a per-piece price
--      (95 × 1000 / 500 g = 190 ₪/kg) although the plugin's unitPrice is already ₪/kg. Code fix:
--      orderItemLineDisplay.ts honours lineQuantityType = "kg".
--
-- ORDER OF OPERATIONS (important):
--   A. Run this script (sections 1-3) and COMMIT.
--   B. Deploy backend + frontend fixes.
--   C. Only THEN trash the duplicate Woo products in WP admin: 43933 (lavrak-2), 43932 (bar-yam-2),
--      44123 (dennis-2). Trashing them before step A would make the next sync get a 404 on the copy and
--      create yet another duplicate.
--   D. Refund the overcharged paid orders (section 4 lists the amounts) — Cardcom partial refund per order.

------------------------------------------------------------------------------------------------
-- 0. Preview
------------------------------------------------------------------------------------------------
SELECT x.Id, x.ProductId, p.Name, p.WooCommerceId AS OriginalWooId, x.WooCommerceProductId AS DuplicateWooId
FROM ProductSiteWooId x JOIN Product p ON p.Id = x.ProductId
WHERE x.SiteId = 45 AND x.ProductId IN (11921, 11922, 11941);

-- Unlinked lines and the variant each will be linked to (by VariantTitle = option values joined " | ",
-- ordered by option name — same order the Woo payload attributes were joined in).
SELECT oi.Id AS LineId, o.OrderNumber, oi.Title, oi.VariantTitle, oi.WooCommerceProductId,
       p.Id AS ProductId,
       (SELECT TOP 1 pv.Id FROM ProductVariant pv
        WHERE pv.ProductId = p.Id AND pv.IsDeleted = 0
          AND (SELECT STRING_AGG(LTRIM(RTRIM(v.OptionValue)), ' | ') WITHIN GROUP (ORDER BY v.OptionName)
               FROM ProductVariantOptionValue v WHERE v.ProductVariantId = pv.Id) = LTRIM(RTRIM(oi.VariantTitle))
        ORDER BY pv.Id) AS VariantId
FROM OrderItem oi
JOIN [Order] o ON o.Id = oi.OrderId
JOIN Product p ON p.WooCommerceId = oi.WooCommerceProductId AND p.IsDeleted = 0 AND p.AccountId = 42
WHERE o.SiteId = 45 AND oi.ProductId IS NULL AND oi.WooCommerceProductId IN (16169, 15939, 15504)
ORDER BY o.OrderNumber;

------------------------------------------------------------------------------------------------
-- 1. THE FIX (check the counts, then COMMIT)
------------------------------------------------------------------------------------------------
BEGIN TRAN;

-- 1a. Point the per-site map back at the ORIGINAL Woo products (the ones still being sold)
UPDATE x SET x.WooCommerceProductId = p.WooCommerceId
FROM ProductSiteWooId x JOIN Product p ON p.Id = x.ProductId
WHERE x.SiteId = 45 AND x.ProductId IN (11921, 11922, 11941) AND x.WooCommerceProductId <> p.WooCommerceId;
SELECT @@ROWCOUNT AS SiteWooIdsRepointed;     -- expect 3

-- 1b. Live variants carry variation ids of the DUPLICATE products. Clear them so the next sync
--     re-resolves variations under the original product by attribute signature (no 404s / no new copies).
DELETE FROM ProductSiteVariantWooId
WHERE SiteId = 45 AND ProductId IN (11921, 11922, 11941);
SELECT @@ROWCOUNT AS SiteVariantWooIdsRemoved;  -- expect 0 (none existed)

UPDATE ProductVariant SET WooCommerceVariationId = NULL
WHERE ProductId IN (11921, 11922, 11941) AND IsDeleted = 0 AND WooCommerceVariationId IS NOT NULL;
SELECT @@ROWCOUNT AS VariantWooIdsCleared;

-- 1c. Re-link the unlinked order lines (product, variant by title, per-piece weight from the plugin payload)
UPDATE oi
SET oi.ProductId = p.Id,
    oi.ProductVariantId = (SELECT TOP 1 pv.Id FROM ProductVariant pv
        WHERE pv.ProductId = p.Id AND pv.IsDeleted = 0
          AND (SELECT STRING_AGG(LTRIM(RTRIM(v.OptionValue)), ' | ') WITHIN GROUP (ORDER BY v.OptionName)
               FROM ProductVariantOptionValue v WHERE v.ProductVariantId = pv.Id) = LTRIM(RTRIM(oi.VariantTitle))
        ORDER BY pv.Id),
    oi.UnitWeightGrams = COALESCE(oi.UnitWeightGrams, oi.LineUnitWeightKg * 1000)
FROM OrderItem oi
JOIN [Order] o ON o.Id = oi.OrderId
JOIN Product p ON p.WooCommerceId = oi.WooCommerceProductId AND p.IsDeleted = 0 AND p.AccountId = 42
WHERE o.SiteId = 45 AND oi.ProductId IS NULL AND oi.WooCommerceProductId IN (16169, 15939, 15504);
SELECT @@ROWCOUNT AS LinesRelinked;            -- expect 13

-- COMMIT;
-- ROLLBACK;

------------------------------------------------------------------------------------------------
-- 2. Verify: resolution now succeeds for the original Woo ids (mirrors GetProductIdByWooCommerceIdAndSiteAsync)
--    and no unlinked lines remain for these products.
------------------------------------------------------------------------------------------------
SELECT w.WooId, (SELECT p.Id FROM Product p
                 WHERE p.IsDeleted = 0 AND p.WooCommerceId = w.WooId
                   AND EXISTS (SELECT 1 FROM ProductSite ps WHERE ps.ProductId = p.Id AND ps.SiteId = 45)
                   AND NOT EXISTS (SELECT 1 FROM ProductSiteWooId x WHERE x.ProductId = p.Id AND x.SiteId = 45 AND x.WooCommerceProductId <> w.WooId)) AS ResolvesTo
FROM (VALUES (16169), (15939), (15504)) w(WooId);

SELECT oi.Id, o.OrderNumber FROM OrderItem oi JOIN [Order] o ON o.Id = oi.OrderId
WHERE o.SiteId = 45 AND oi.ProductId IS NULL AND oi.WooCommerceProductId IN (16169, 15939, 15504);

------------------------------------------------------------------------------------------------
-- 3. Open (unpaid) orders whose lines were already picked at the wrong rate: recompute the line and the order
--    totals so they are charged correctly. Unpicked open orders (1073, 1144, 1149) need nothing — picking
--    after the deploy computes the right rate. (Run inside the same transaction or a new one.)
------------------------------------------------------------------------------------------------
BEGIN TRAN;
;WITH wrong AS (
    SELECT oi.Id, oi.OrderId, oi.TotalPrice AS OldTotal,
           CAST(oi.PickedQuantity * oi.PricePerUnit AS decimal(18,2)) AS NewTotal
    FROM OrderItem oi JOIN [Order] o ON o.Id = oi.OrderId
    WHERE o.SiteId = 45 AND o.PaymentStatus = 'Unpaid' AND oi.PickedQuantity > 0
      AND oi.WooCommerceProductId IN (16169, 15939, 15504)
      AND oi.LineQuantityType = 'kg' AND oi.LineUnitWeightKg > 0
      AND ABS(oi.TotalPrice - oi.PickedQuantity * oi.PricePerUnit) > 0.05
)
UPDATE o SET o.SubTotal = o.SubTotal - d.Delta, o.Total = o.Total - d.Delta
FROM [Order] o JOIN (SELECT OrderId, SUM(OldTotal - NewTotal) AS Delta FROM wrong GROUP BY OrderId) d ON d.OrderId = o.Id;
SELECT @@ROWCOUNT AS OpenOrdersAdjusted;        -- expect 1 (order 1074: 112.10 -> 56.05)

UPDATE oi SET oi.TotalPrice = CAST(oi.PickedQuantity * oi.PricePerUnit AS decimal(18,2))
FROM OrderItem oi JOIN [Order] o ON o.Id = oi.OrderId
WHERE o.SiteId = 45 AND o.PaymentStatus = 'Unpaid' AND oi.PickedQuantity > 0
  AND oi.WooCommerceProductId IN (16169, 15939, 15504)
  AND oi.LineQuantityType = 'kg' AND oi.LineUnitWeightKg > 0
  AND ABS(oi.TotalPrice - oi.PickedQuantity * oi.PricePerUnit) > 0.05;
SELECT @@ROWCOUNT AS OpenLinesRecomputed;      -- expect 1
-- COMMIT;
-- ROLLBACK;

------------------------------------------------------------------------------------------------
-- 4. PAID orders overcharged (for refunds — NOT modified here; the charge already happened in Cardcom).
--    Overcharge = charged line − picked kg × ₪/kg.
------------------------------------------------------------------------------------------------
SELECT o.OrderNumber, o.CustomerName, o.CustomerPhone, o.Total AS ChargedTotal,
       oi.Title, oi.PickedQuantity AS PickedKg, oi.PricePerUnit AS PerKg,
       oi.TotalPrice AS ChargedLine,
       CAST(oi.PickedQuantity * oi.PricePerUnit AS decimal(18,2)) AS CorrectLine,
       CAST(oi.TotalPrice - oi.PickedQuantity * oi.PricePerUnit AS decimal(18,2)) AS RefundDue
FROM OrderItem oi JOIN [Order] o ON o.Id = oi.OrderId
WHERE o.SiteId = 45 AND o.PaymentStatus = 'Paid' AND oi.PickedQuantity > 0
  AND oi.WooCommerceProductId IN (16169, 15939, 15504)
  AND oi.LineQuantityType = 'kg' AND oi.LineUnitWeightKg > 0
  AND ABS(oi.TotalPrice - oi.PickedQuantity * oi.PricePerUnit) > 0.05
ORDER BY o.OrderNumber;
-- Expected (23/08 12:45): 1076 195.70 | 1091 32.40 | 1125 99.75 | 1128 1017.45 | 1138 200.45 | 1141 44.89+95.00 → total ≈ 1,685.64 ₪

------------------------------------------------------------------------------------------------
-- 5. ADDENDUM 23/08 14:00 — product 12113 (פילה אנטיאס טרי – לוין – נתח לסשימי) is the SAME bug.
--    Its Woo slug is "טונה-אדומה-...-עותק" (created in George as a copy of the tuna product, renamed later;
--    Woo keeps the original slug), which is why it was first mistaken for a legitimate George copy.
--    Original = 41831 (in stock, stale since 23/8 10:30); duplicate = 44147 (receives all syncs, closed).
--    Same order of operations: run + COMMIT → trash 44147 in WP admin → sync the product from George.
------------------------------------------------------------------------------------------------
BEGIN TRAN;

UPDATE ProductSiteWooId SET WooCommerceProductId = 41831
WHERE SiteId = 45 AND ProductId = 12113 AND WooCommerceProductId = 44147;
SELECT @@ROWCOUNT AS AntiasSiteWooIdRepointed;   -- expect 1

DELETE FROM ProductSiteVariantWooId WHERE SiteId = 45 AND ProductId = 12113;
UPDATE ProductVariant SET WooCommerceVariationId = NULL
WHERE ProductId = 12113 AND IsDeleted = 0 AND WooCommerceVariationId IS NOT NULL;
SELECT @@ROWCOUNT AS AntiasVariantWooIdsCleared;   -- expect 3

-- COMMIT;
-- ROLLBACK;

-- Verify (expect 41831 / 12113 / 0)
SELECT (SELECT WooCommerceProductId FROM ProductSiteWooId WHERE SiteId = 45 AND ProductId = 12113) AS SiteMap,
       (SELECT p.Id FROM Product p WHERE p.IsDeleted = 0 AND p.WooCommerceId = 41831
          AND EXISTS (SELECT 1 FROM ProductSite ps WHERE ps.ProductId = p.Id AND ps.SiteId = 45)
          AND NOT EXISTS (SELECT 1 FROM ProductSiteWooId x WHERE x.ProductId = p.Id AND x.SiteId = 45 AND x.WooCommerceProductId <> 41831)) AS ResolvesTo,
       (SELECT COUNT(*) FROM ProductVariant WHERE ProductId = 12113 AND IsDeleted = 0 AND WooCommerceVariationId IS NOT NULL) AS VarsWithWooId;
