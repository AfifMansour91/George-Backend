-- Variant option values stored as percent-encoded WordPress term SLUGS instead of display names
-- (e.g. "%d7%98%d7%97%d7%99%d7%a0%d7%94-%d7%9b%d7%a4%d7%95%d7%9c%d7%94" = "טחינה-כפולה" instead of
-- "טחינה כפולה"). Woo's variation REST endpoint returns the raw slug when it fails to resolve the term;
-- the import used to store it as-is (JOSEPH: variation dropdown showed gibberish in order building).
--
-- Code fix deployed alongside this script: WooCommerceService import now decodes such values and maps
-- them back to the attribute's display values. This script repairs rows imported before that fix by
-- SLUGIFYING each ProductOptionValue of the same product+option (sanitize_title style: spaces -> '-',
-- non-ASCII percent-encoded UTF-8, lowercase) and matching it to the stored slug.

------------------------------------------------------------------------------------------------
-- 0. Helper: WordPress-style slugify (encode direction — no UTF-8 collation needed)
------------------------------------------------------------------------------------------------
GO
CREATE OR ALTER FUNCTION dbo.fn_WooSlugifyForRepair(@s nvarchar(400))
RETURNS nvarchar(2400)
AS
BEGIN
    DECLARE @out nvarchar(2400) = N'', @i int = 1, @cp int;
    SET @s = LTRIM(RTRIM(@s));
    WHILE @i <= LEN(@s)
    BEGIN
        SET @cp = UNICODE(SUBSTRING(@s, @i, 1));
        IF @cp IN (32, 9) -- whitespace -> hyphen (sanitize_title)
            SET @out += N'-';
        ELSE IF @cp < 128
            SET @out += LOWER(SUBSTRING(@s, @i, 1));
        ELSE IF @cp < 2048 -- 2-byte UTF-8 (covers all Hebrew)
            SET @out += N'%' + LOWER(CONVERT(nvarchar(2), CONVERT(binary(1), 192 | (@cp / 64)), 2))
                      + N'%' + LOWER(CONVERT(nvarchar(2), CONVERT(binary(1), 128 | (@cp % 64)), 2));
        ELSE -- 3-byte UTF-8
            SET @out += N'%' + LOWER(CONVERT(nvarchar(2), CONVERT(binary(1), 224 | (@cp / 4096)), 2))
                      + N'%' + LOWER(CONVERT(nvarchar(2), CONVERT(binary(1), 128 | ((@cp / 64) % 64)), 2))
                      + N'%' + LOWER(CONVERT(nvarchar(2), CONVERT(binary(1), 128 | (@cp % 64)), 2));
        SET @i += 1;
    END
    -- collapse duplicate hyphens, strip leading/trailing (sanitize_title behavior)
    WHILE CHARINDEX(N'--', @out) > 0 SET @out = REPLACE(@out, N'--', N'-');
    IF LEFT(@out, 1) = N'-' SET @out = STUFF(@out, 1, 1, N'');
    IF RIGHT(@out, 1) = N'-' SET @out = LEFT(@out, LEN(@out) - 1);
    RETURN @out;
END
GO

------------------------------------------------------------------------------------------------
-- 1. Preview: encoded rows and the display value each one resolves to.
--    ResolvedValue NULL = no unambiguous match -> NOT updated by step 2; handle manually.
------------------------------------------------------------------------------------------------
SELECT pv.ProductId, p.Name AS ProductName, pvo.ProductVariantId, pvo.OptionName,
       pvo.OptionValue AS EncodedSlug,
       (SELECT MIN(pov.Value)
        FROM ProductOption po
        JOIN ProductOptionValue pov ON pov.ProductOptionId = po.Id
        WHERE po.ProductId = pv.ProductId AND po.IsDeleted = 0
          AND LTRIM(RTRIM(po.Name)) = LTRIM(RTRIM(pvo.OptionName))
          AND dbo.fn_WooSlugifyForRepair(pov.Value) = LOWER(LTRIM(RTRIM(pvo.OptionValue)))
        HAVING COUNT(DISTINCT pov.Value) = 1) AS ResolvedValue
FROM ProductVariantOptionValue pvo
JOIN ProductVariant pv ON pv.Id = pvo.ProductVariantId
JOIN Product p ON p.Id = pv.ProductId
WHERE pvo.OptionValue LIKE '%[%][0-9a-f][0-9a-f]%'
ORDER BY pv.ProductId, pvo.ProductVariantId;

------------------------------------------------------------------------------------------------
-- 2. THE FIX (check the count against the preview's resolved rows, then COMMIT)
------------------------------------------------------------------------------------------------
BEGIN TRAN;

UPDATE pvo
SET pvo.OptionValue = r.ResolvedValue
FROM ProductVariantOptionValue pvo
JOIN ProductVariant pv ON pv.Id = pvo.ProductVariantId
CROSS APPLY (SELECT MIN(pov.Value) AS ResolvedValue
             FROM ProductOption po
             JOIN ProductOptionValue pov ON pov.ProductOptionId = po.Id
             WHERE po.ProductId = pv.ProductId AND po.IsDeleted = 0
               AND LTRIM(RTRIM(po.Name)) = LTRIM(RTRIM(pvo.OptionName))
               AND dbo.fn_WooSlugifyForRepair(pov.Value) = LOWER(LTRIM(RTRIM(pvo.OptionValue)))
             HAVING COUNT(DISTINCT pov.Value) = 1) r
WHERE pvo.OptionValue LIKE '%[%][0-9a-f][0-9a-f]%'
  AND r.ResolvedValue IS NOT NULL;
SELECT @@ROWCOUNT AS SlugsRepaired;

-- COMMIT;
-- ROLLBACK;

------------------------------------------------------------------------------------------------
-- 3. Verify: remaining encoded rows (rows the slug-match could not resolve — e.g. the product has
--    no ProductOptionValue rows at all). These need the Woo term names / a re-import of the product.
------------------------------------------------------------------------------------------------
SELECT pv.ProductId, pvo.ProductVariantId, pvo.OptionName, pvo.OptionValue
FROM ProductVariantOptionValue pvo
JOIN ProductVariant pv ON pv.Id = pvo.ProductVariantId
WHERE pvo.OptionValue LIKE '%[%][0-9a-f][0-9a-f]%'
ORDER BY pv.ProductId;

-- Optional cleanup once done:
-- DROP FUNCTION dbo.fn_WooSlugifyForRepair;
