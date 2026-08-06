-- Scan (read-only sections A-C): find ALL products where live variations will be skipped by Woo sync
-- because their option rows are missing/soft-deleted — same defect as product 12690
-- ("selected_site edit dropped options" footprint).
-- A variation is skipped when it has no ProductVariantOptionValue rows, or when its OptionName
-- has no matching non-deleted ProductOption on the product.

------------------------------------------------------------------------------------------------
-- A. Affected products (one row per product, with reason breakdown)
------------------------------------------------------------------------------------------------
;WITH VariantFlags AS (
    -- per-variant flags first; SQL Server forbids subqueries inside aggregates
    SELECT p.Id AS ProductId, p.Name AS ProductName, p.AccountId, pv.Id AS VariantId,
           CASE WHEN EXISTS (SELECT 1 FROM ProductVariantOptionValue x
                             WHERE x.ProductVariantId = pv.Id
                               AND LTRIM(RTRIM(x.OptionName)) <> '')
                THEN 1 ELSE 0 END AS HasOptionRows,
           CASE WHEN EXISTS (SELECT 1 FROM ProductVariantOptionValue x
                             JOIN ProductOption po ON po.ProductId = p.Id AND po.IsDeleted = 0
                                  AND LTRIM(RTRIM(po.Name)) = LTRIM(RTRIM(x.OptionName))
                             WHERE x.ProductVariantId = pv.Id)
                THEN 1 ELSE 0 END AS HasLiveOptionMatch
    FROM Product p
    JOIN ProductVariant pv ON pv.ProductId = p.Id AND pv.IsDeleted = 0
    WHERE p.IsDeleted = 0
)
SELECT ProductId, ProductName, AccountId,
       COUNT(*) AS LiveVariants,
       SUM(CASE WHEN HasOptionRows = 0 THEN 1 ELSE 0 END) AS VariantsWithNoOptionRows,
       SUM(CASE WHEN HasOptionRows = 1 AND HasLiveOptionMatch = 0 THEN 1 ELSE 0 END) AS VariantsWithDeletedOption
FROM VariantFlags
GROUP BY ProductId, ProductName, AccountId
HAVING SUM(CASE WHEN HasOptionRows = 0 OR HasLiveOptionMatch = 0 THEN 1 ELSE 0 END) > 0
ORDER BY AccountId, ProductId;

------------------------------------------------------------------------------------------------
-- B. Restore candidates: newest soft-deleted ProductOption whose name the live variants still use
--    (only for products where NO live option with that name exists)
------------------------------------------------------------------------------------------------
;WITH ReferencedNames AS (
    SELECT DISTINCT pv.ProductId, LTRIM(RTRIM(pvo.OptionName)) AS OptionName
    FROM ProductVariant pv
    JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
    WHERE pv.IsDeleted = 0 AND LTRIM(RTRIM(pvo.OptionName)) <> ''
), Missing AS (
    SELECT rn.ProductId, rn.OptionName
    FROM ReferencedNames rn
    WHERE NOT EXISTS (SELECT 1 FROM ProductOption po
                      WHERE po.ProductId = rn.ProductId AND po.IsDeleted = 0
                        AND LTRIM(RTRIM(po.Name)) = rn.OptionName)
)
SELECT m.ProductId, m.OptionName,
       (SELECT MAX(po.Id) FROM ProductOption po
        WHERE po.ProductId = m.ProductId AND po.IsDeleted = 1
          AND LTRIM(RTRIM(po.Name)) = m.OptionName) AS NewestDeletedOptionId
FROM Missing m
ORDER BY m.ProductId;

------------------------------------------------------------------------------------------------
-- C. Value-coverage check for the candidates from B: variant option VALUES that the candidate
--    option's ProductOptionValue rows do NOT contain. Rows here = after restoring, either fix
--    these ProductOptionValue rows or DELETE the option's value rows (sync then derives values
--    from the variants themselves — GetProductOptionValuesForWooSync fallback).
------------------------------------------------------------------------------------------------
;WITH Candidate AS (
    SELECT po.ProductId, LTRIM(RTRIM(po.Name)) AS OptionName, MAX(po.Id) AS OptionId
    FROM ProductOption po
    WHERE po.IsDeleted = 1
      AND NOT EXISTS (SELECT 1 FROM ProductOption live
                      WHERE live.ProductId = po.ProductId AND live.IsDeleted = 0
                        AND LTRIM(RTRIM(live.Name)) = LTRIM(RTRIM(po.Name)))
      AND EXISTS (SELECT 1 FROM ProductVariant pv
                  JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
                  WHERE pv.ProductId = po.ProductId AND pv.IsDeleted = 0
                    AND LTRIM(RTRIM(pvo.OptionName)) = LTRIM(RTRIM(po.Name)))
    GROUP BY po.ProductId, LTRIM(RTRIM(po.Name))
)
SELECT c.ProductId, c.OptionId, c.OptionName,
       LTRIM(RTRIM(pvo.OptionValue)) AS VariantValueMissingFromOption
FROM Candidate c
JOIN ProductVariant pv ON pv.ProductId = c.ProductId AND pv.IsDeleted = 0
JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
     AND LTRIM(RTRIM(pvo.OptionName)) = c.OptionName
WHERE LTRIM(RTRIM(pvo.OptionValue)) <> ''
  AND NOT EXISTS (SELECT 1 FROM ProductOptionValue pov
                  WHERE pov.ProductOptionId = c.OptionId
                    AND LTRIM(RTRIM(pov.Value)) = LTRIM(RTRIM(pvo.OptionValue)))
GROUP BY c.ProductId, c.OptionId, c.OptionName, LTRIM(RTRIM(pvo.OptionValue))
ORDER BY c.ProductId;

------------------------------------------------------------------------------------------------
-- D. BULK FIX (commented out — review A-C first, then run inside the transaction):
--    restores the newest deleted option per (product, referenced name) from section B.
------------------------------------------------------------------------------------------------
/*
BEGIN TRAN;
;WITH ReferencedNames AS (
    SELECT DISTINCT pv.ProductId, LTRIM(RTRIM(pvo.OptionName)) AS OptionName
    FROM ProductVariant pv
    JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
    WHERE pv.IsDeleted = 0 AND LTRIM(RTRIM(pvo.OptionName)) <> ''
), Missing AS (
    SELECT rn.ProductId, rn.OptionName
    FROM ReferencedNames rn
    WHERE NOT EXISTS (SELECT 1 FROM ProductOption po
                      WHERE po.ProductId = rn.ProductId AND po.IsDeleted = 0
                        AND LTRIM(RTRIM(po.Name)) = rn.OptionName)
), ToRestore AS (
    SELECT m.ProductId, m.OptionName,
           (SELECT MAX(po.Id) FROM ProductOption po
            WHERE po.ProductId = m.ProductId AND po.IsDeleted = 1
              AND LTRIM(RTRIM(po.Name)) = m.OptionName) AS OptionId
    FROM Missing m
)
UPDATE po SET po.IsDeleted = 0
FROM ProductOption po
JOIN ToRestore t ON t.OptionId = po.Id;

SELECT @@ROWCOUNT AS OptionsRestored;
-- COMMIT;   -- OptionsRestored must equal the count of section-B rows with a non-NULL
--              NewestDeletedOptionId (rows with NULL have nothing to restore and are untouched)
-- ROLLBACK;
*/
