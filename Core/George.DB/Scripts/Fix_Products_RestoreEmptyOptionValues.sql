-- Active ProductOption rows with NO ProductOptionValue rows while the product's active variants still carry
-- values for that option. The edit form draws the "ערכי תכונה" chips from ProductOptionValue, so with the list
-- empty no variant can be removed and the option looks blank; the Woo sync only survives because it falls back
-- to the variants' values (GetProductOptionValuesForWooSync).
-- Meshek Basar (account 44) 2026-09-27: 7 products (12673, 12676, 12678, 12681, 12682, 12689, 12702) whose
-- "צורת חיתוך" was saved with values: [] on 2026-07-27 05:20-05:24 UTC (the same per-site saves that hid every
-- cut but "נתח שלם" on the PT site). Same state on 2 products of account 17 and 3 of account 42.
-- Repair: per (product, active option) add every distinct value its active variants use.
-- Companion to Fix_Products_RestoreVariationOptions.sql (which handles options that are soft-deleted / missing).
--
-- Run block 1 (preview), then block 2 inside the transaction, check the counts, then COMMIT manually.
-- Block 3 is OPTIONAL and Meshek Basar specific - see its comment before running it.

-- ===== 1. Preview =====
;WITH needed AS (
    SELECT DISTINCT o.Id AS OptionId, o.ProductId, o.Name AS OptionName, ov.OptionValue
    FROM dbo.ProductOption o
    JOIN dbo.Product p ON p.Id = o.ProductId AND p.IsDeleted = 0
    JOIN dbo.ProductVariant v ON v.ProductId = o.ProductId AND v.IsDeleted = 0
    JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
        AND LOWER(LTRIM(RTRIM(ov.OptionName))) = LOWER(LTRIM(RTRIM(o.Name)))
    WHERE o.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM dbo.ProductOptionValue pv WHERE pv.ProductOptionId = o.Id)
)
SELECT n.ProductId, p.AccountId, p.Name AS Product, n.OptionId, n.OptionName,
    STRING_AGG(n.OptionValue, ' | ') AS ValuesFromVariants, COUNT(*) AS ValueCount
FROM needed n JOIN dbo.Product p ON p.Id = n.ProductId
GROUP BY n.ProductId, p.AccountId, p.Name, n.OptionId, n.OptionName
ORDER BY p.AccountId, n.ProductId;
-- expect (2026-09-27): 12 rows - account 17: 3901, 3994 / account 42: 11863, 11864, 11885 /
--   account 44: 12673 (4), 12676 (5), 12678 (4), 12681 (4), 12682 (3), 12689 (2), 12702 (4)

-- ===== 2. Apply (COMMIT manually after checking the counts) =====
BEGIN TRAN;

INSERT INTO dbo.ProductOptionValue (ProductOptionId, Value)
SELECT DISTINCT o.Id, ov.OptionValue
FROM dbo.ProductOption o
JOIN dbo.Product p ON p.Id = o.ProductId AND p.IsDeleted = 0
JOIN dbo.ProductVariant v ON v.ProductId = o.ProductId AND v.IsDeleted = 0
JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
    AND LOWER(LTRIM(RTRIM(ov.OptionName))) = LOWER(LTRIM(RTRIM(o.Name)))
WHERE o.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductOptionValue pv WHERE pv.ProductOptionId = o.Id);
SELECT @@ROWCOUNT AS values_added;             -- expect the sum of ValueCount in the preview (account 44 alone: 26)

-- Verify: no active option left empty while its variants still use values.
SELECT o.Id, o.ProductId, o.Name
FROM dbo.ProductOption o
JOIN dbo.Product p ON p.Id = o.ProductId AND p.IsDeleted = 0
WHERE o.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductOptionValue pv WHERE pv.ProductOptionId = o.Id)
  AND EXISTS (SELECT 1 FROM dbo.ProductVariant v JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
              WHERE v.ProductId = o.ProductId AND v.IsDeleted = 0
                AND LOWER(LTRIM(RTRIM(ov.OptionName))) = LOWER(LTRIM(RTRIM(o.Name))));   -- expect no rows

SELECT o.Id, o.ProductId, o.Name, (SELECT STRING_AGG(pv.Value, ' | ') FROM dbo.ProductOptionValue pv WHERE pv.ProductOptionId = o.Id) AS Vals
FROM dbo.ProductOption o
WHERE o.IsDeleted = 0 AND o.ProductId IN (12673, 12676, 12678, 12681, 12682, 12689, 12702);

-- ROLLBACK;
-- COMMIT;

-- ===== 3. OPTIONAL (Meshek Basar only): show every cut on the PT site (site 48) too =====
-- The PT site hides every variant except "נתח שלם" on the same 7 products (ProductSiteVariantStock.IsExcluded = 1,
-- written 2026-07-27 05:20-05:24 UTC by per-site saves). The PT WooCommerce store has exactly one variation per
-- product for the same reason. Run this block ONLY if the shop wants PT to sell the same cuts as Rehovot; then
-- SAVE each of the 7 products in George (or run a site sync for PT) so the missing PT variations get created.
-- 12672 "שייטל" and 12700 "כתף מרכזי" also hide cuts on PT but carry PT-specific variants - left untouched here.
-- Preview:
SELECT s.Id, s.ProductId, p.Name, s.ProductVariantId, ov.OptionValue, s.IsExcluded, s.CreationTime
FROM dbo.ProductSiteVariantStock s
JOIN dbo.ProductVariant v ON v.Id = s.ProductVariantId AND v.IsDeleted = 0
JOIN dbo.Product p ON p.Id = v.ProductId
LEFT JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
WHERE s.SiteId = 48 AND s.IsExcluded = 1 AND s.IsDeleted = 0
  AND v.ProductId IN (12673, 12676, 12678, 12681, 12682, 12689, 12702)
ORDER BY s.ProductId, s.ProductVariantId;
-- expect 19 rows: 12673 (3), 12676 (4), 12678 (3), 12681 (3), 12682 (2), 12689 (1), 12702 (3)

-- Apply (COMMIT manually):
-- BEGIN TRAN;
-- UPDATE s SET s.IsExcluded = 0, s.UpdatedDate = GETUTCDATE()
-- FROM dbo.ProductSiteVariantStock s
-- JOIN dbo.ProductVariant v ON v.Id = s.ProductVariantId AND v.IsDeleted = 0
-- WHERE s.SiteId = 48 AND s.IsExcluded = 1 AND s.IsDeleted = 0
--   AND v.ProductId IN (12673, 12676, 12678, 12681, 12682, 12689, 12702);
-- SELECT @@ROWCOUNT AS variants_unhidden_on_pt;   -- expect 19
-- ROLLBACK;
-- COMMIT;
