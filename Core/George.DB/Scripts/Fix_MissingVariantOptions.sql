-- Fix for the 05/08 scan findings (Scan_VariantsWithDeletedOption.sql).
-- Cases and actions:
--   12726  (צלעות טלה)      -> restore deleted option 8504 (same as product 12690).
--   11314/11573/11681/11887 -> variants reference "צורת-חיתוך" but NO such ProductOption ever
--      (קרפיון לטחינה)         existed. Standardize name to "צורת חיתוך" (space, matches the rest
--                              of the catalog / shared Woo global attribute) and CREATE the option.
--                              No ProductOptionValue rows are inserted on purpose: the sync's
--                              GetProductOptionValuesForWooSync fallback derives the values from
--                              the variants, guaranteed consistent.
--   13500  (המבורגר PEPE)   -> same, option "trays" never existed; create it with the exact name
--                              the variants use.
--   11863/11864/11885        -> NOT fixed here: variants have no option-value rows at all and
--      (דגים, 70/70/25)        ProductVariant has no name column to reconstruct from. See the
--                              check at the bottom; needs Woo-side data / re-import to rebuild.

------------------------------------------------------------------------------------------------
-- 0. Preview: current variant option rows for the products being fixed
------------------------------------------------------------------------------------------------
SELECT pv.ProductId, pvo.ProductVariantId, pvo.OptionName, pvo.OptionValue
FROM ProductVariant pv
JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
WHERE pv.IsDeleted = 0
  AND pv.ProductId IN (11314, 11573, 11681, 11887, 12726, 13500)
ORDER BY pv.ProductId, pvo.ProductVariantId;

------------------------------------------------------------------------------------------------
-- 1. THE FIX (one transaction; check the three result counts, then COMMIT)
------------------------------------------------------------------------------------------------
BEGIN TRAN;

-- 1a. Restore 12726's newest deleted "צורת חיתוך" option (value coverage already verified: scan section C was empty)
UPDATE ProductOption SET IsDeleted = 0
WHERE Id = 8504 AND ProductId = 12726;
SELECT @@ROWCOUNT AS Restored_12726;      -- expect 1

-- 1b. קרפיון products: rename variant rows "צורת-חיתוך" -> "צורת חיתוך"
--     (OptionName is part of the PK; guard against an existing row with the target name)
UPDATE pvo SET pvo.OptionName = N'צורת חיתוך'
FROM ProductVariantOptionValue pvo
JOIN ProductVariant pv ON pv.Id = pvo.ProductVariantId
WHERE pv.ProductId IN (11314, 11573, 11681, 11887)
  AND LTRIM(RTRIM(pvo.OptionName)) = N'צורת-חיתוך'
  AND NOT EXISTS (SELECT 1 FROM ProductVariantOptionValue dup
                  WHERE dup.ProductVariantId = pvo.ProductVariantId
                    AND dup.OptionName = N'צורת חיתוך');
SELECT @@ROWCOUNT AS Renamed_PVOV;        -- expect 16 (4 products x 4 variants)

-- 1c. Create the missing options (no ProductOptionValue rows: sync derives values from variants)
INSERT INTO ProductOption (ProductId, Name, IsDeleted)
SELECT v.ProductId, v.OptionName, 0
FROM (VALUES (11314, N'צורת חיתוך'),
             (11573, N'צורת חיתוך'),
             (11681, N'צורת חיתוך'),
             (11887, N'צורת חיתוך'),
             (13500, N'trays')) v(ProductId, OptionName)
WHERE NOT EXISTS (SELECT 1 FROM ProductOption po
                  WHERE po.ProductId = v.ProductId AND po.IsDeleted = 0
                    AND LTRIM(RTRIM(po.Name)) = v.OptionName);
SELECT @@ROWCOUNT AS OptionsCreated;      -- expect 5

-- COMMIT;
-- ROLLBACK;

------------------------------------------------------------------------------------------------
-- 2. Verify: re-run the skip check for the fixed products - should return ZERO rows after COMMIT
------------------------------------------------------------------------------------------------
SELECT pv.ProductId, pv.Id AS VariantId
FROM ProductVariant pv
WHERE pv.IsDeleted = 0
  AND pv.ProductId IN (11314, 11573, 11681, 11887, 12726, 13500)
  AND NOT EXISTS (SELECT 1
                  FROM ProductVariantOptionValue pvo
                  JOIN ProductOption po ON po.ProductId = pv.ProductId AND po.IsDeleted = 0
                       AND LTRIM(RTRIM(po.Name)) = LTRIM(RTRIM(pvo.OptionName))
                  WHERE pvo.ProductVariantId = pv.Id);

------------------------------------------------------------------------------------------------
-- 3. The fish products (11863/11864/11885): were their variants imported from Woo?
--    If WithWooId > 0, the variations exist on the Woo side and can be re-imported to rebuild
--    the option-value rows; George-side data alone cannot reconstruct them.
------------------------------------------------------------------------------------------------
SELECT pv.ProductId,
       COUNT(*) AS LiveVariants,
       SUM(CASE WHEN pv.WooCommerceVariationId IS NOT NULL THEN 1 ELSE 0 END) AS WithWooId
FROM ProductVariant pv
WHERE pv.IsDeleted = 0 AND pv.ProductId IN (11863, 11864, 11885)
GROUP BY pv.ProductId;
