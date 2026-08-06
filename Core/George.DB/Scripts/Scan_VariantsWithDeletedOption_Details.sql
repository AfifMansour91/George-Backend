-- Follow-up details for the products flagged by Scan_VariantsWithDeletedOption.sql (run 05/08).
-- Three distinct cases showed up; this script separates them and preps the targeted fixes.

DECLARE @Flagged TABLE (ProductId INT PRIMARY KEY);
INSERT INTO @Flagged VALUES (11314),(11573),(11681),(11863),(11864),(11885),(11887),(12726),(13500);

------------------------------------------------------------------------------------------------
-- 1. Per-product picture: setup type, live + deleted option names, and the option names the
--    live variants actually reference. Decide each product's fix from this.
------------------------------------------------------------------------------------------------
SELECT p.Id AS ProductId, p.Name AS ProductName, p.AccountId, st.Name AS SetupType,
       (SELECT STRING_AGG(po.Name, ' | ') FROM ProductOption po
        WHERE po.ProductId = p.Id AND po.IsDeleted = 0)  AS LiveOptions,
       (SELECT STRING_AGG(po.Name, ' | ') FROM ProductOption po
        WHERE po.ProductId = p.Id AND po.IsDeleted = 1)  AS DeletedOptions
FROM Product p
LEFT JOIN SetupType st ON st.Id = p.SetupTypeId
WHERE p.Id IN (SELECT ProductId FROM @Flagged);

SELECT pv.ProductId, LTRIM(RTRIM(pvo.OptionName)) AS ReferencedOptionName,
       COUNT(DISTINCT pv.Id) AS LiveVariantsUsingIt
FROM ProductVariant pv
JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
WHERE pv.IsDeleted = 0 AND pv.ProductId IN (SELECT ProductId FROM @Flagged)
GROUP BY pv.ProductId, LTRIM(RTRIM(pvo.OptionName))
ORDER BY pv.ProductId;

------------------------------------------------------------------------------------------------
-- 2. Hyphen/space mismatch ("צורת-חיתוך" on variants vs "צורת חיתוך" as option):
--    PREVIEW of PVOV rows whose name matches a live option after hyphen->space replacement.
------------------------------------------------------------------------------------------------
SELECT pvo.ProductVariantId, pvo.OptionName AS CurrentName, po.Name AS LiveOptionName, pvo.OptionValue
FROM ProductVariant pv
JOIN ProductVariantOptionValue pvo ON pvo.ProductVariantId = pv.Id
JOIN ProductOption po ON po.ProductId = pv.ProductId AND po.IsDeleted = 0
     AND LTRIM(RTRIM(po.Name)) = LTRIM(RTRIM(REPLACE(pvo.OptionName, '-', ' ')))
     AND LTRIM(RTRIM(po.Name)) <> LTRIM(RTRIM(pvo.OptionName))
WHERE pv.IsDeleted = 0 AND pv.ProductId IN (SELECT ProductId FROM @Flagged);

------------------------------------------------------------------------------------------------
-- 3. FIX for case 2 (commented; run after the preview looks right): rename the variant option
--    rows to the live option's exact name. OptionName is part of the PK, so guard against an
--    already-existing row with the target name.
------------------------------------------------------------------------------------------------
/*
BEGIN TRAN;
UPDATE pvo SET pvo.OptionName = po.Name
FROM ProductVariantOptionValue pvo
JOIN ProductVariant pv ON pv.Id = pvo.ProductVariantId AND pv.IsDeleted = 0
JOIN ProductOption po ON po.ProductId = pv.ProductId AND po.IsDeleted = 0
     AND LTRIM(RTRIM(po.Name)) = LTRIM(RTRIM(REPLACE(pvo.OptionName, '-', ' ')))
     AND LTRIM(RTRIM(po.Name)) <> LTRIM(RTRIM(pvo.OptionName))
WHERE pv.ProductId IN (SELECT ProductId FROM (VALUES (11314),(11573),(11681),(11887)) f(ProductId))
  AND NOT EXISTS (SELECT 1 FROM ProductVariantOptionValue dup
                  WHERE dup.ProductVariantId = pvo.ProductVariantId
                    AND dup.OptionName = po.Name);
SELECT @@ROWCOUNT AS RowsRenamed;
-- COMMIT;
-- ROLLBACK;
*/
