-- Diagnostic (read-only): why variations 21856/21857 of product 12690 are skipped by Woo sync.
-- The sync skips a variation when it can't build any attribute pair, i.e. when
--   (a) the variant has no ProductVariantOptionValue rows (or blank OptionName), or
--   (b) its OptionName doesn't match any non-deleted ProductOption name on the product.

DECLARE @ProductId INT = 12690;

-- 1. Product options (these become Woo global attributes; keys of attributeMap)
SELECT po.Id, po.Name, po.IsDeleted,
       (SELECT COUNT(*) FROM ProductOptionValue pov WHERE pov.ProductOptionId = po.Id) AS OptionValueCount
FROM ProductOption po
WHERE po.ProductId = @ProductId;

-- 2. Variants and their option-value rows (must match an option name above, after trim/space-collapse)
SELECT pv.Id AS VariantId, pv.Sku, pv.IsDeleted, pv.Weight,
       pvo.OptionName, pvo.OptionValue
FROM ProductVariant pv
LEFT JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
WHERE pv.ProductId = @ProductId
ORDER BY pv.Id;

-- 3. Quick verdict per variant
SELECT pv.Id AS VariantId,
       CASE
         WHEN NOT EXISTS (SELECT 1 FROM ProductVariantOptionValue pvo
                          WHERE pvo.ProductVariantId = pv.Id
                            AND LTRIM(RTRIM(pvo.OptionName)) <> '')
           THEN 'NO OPTION-VALUE ROWS -> skipped'
         WHEN NOT EXISTS (SELECT 1 FROM ProductVariantOptionValue pvo
                          JOIN ProductOption po ON po.ProductId = pv.ProductId AND po.IsDeleted = 0
                          WHERE pvo.ProductVariantId = pv.Id
                            AND LTRIM(RTRIM(pvo.OptionName)) = LTRIM(RTRIM(po.Name)))
           THEN 'OPTION NAME MISMATCH vs ProductOption -> skipped'
         ELSE 'OK - should sync'
       END AS Verdict
FROM ProductVariant pv
WHERE pv.ProductId = @ProductId AND pv.IsDeleted = 0;
