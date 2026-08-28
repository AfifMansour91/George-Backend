-- Percent-encoded Hebrew Woo term slugs stored as text → decode to readable Hebrew.
--
-- Why: Woo's variation REST endpoint returns the term SLUG when it fails to resolve a taxonomy term;
-- Hebrew slugs are percent-encoded by sanitize_title ("%d7%9c%d7%9c%d7%90-%d7%a2%d7%95%d7%a8" = "ללא-עור").
-- Catalog imports before commit b40d9d5 (2026-08-20) stored these raw in ProductVariantOptionValue.OptionValue,
-- and every Woo order line built from such a variant copied the gibberish into OrderItem.OrderLineCuttingLabel /
-- OrderLineSizeLabel / VariantTitle and LineDisplayJson ($.cuttingName / $.sizeName). Seen at דגי גת (account 47,
-- order 5832 item 12133) and 4 more accounts - 167 catalog rows, ~14 order lines as of 2026-08-25.
-- Backend now decodes defensively on ingest/display (WooPercentEncodedText); this script fixes existing rows.
--
-- Safe to re-run: rows without %XX sequences are untouched. Run on George.Prod (and QA if desired).

------------------------------------------------------------------------------------------------
-- 0. Decoder: %XX percent-encoding → UTF-8 bytes → NVARCHAR (handles ASCII + 2/3-byte UTF-8).
------------------------------------------------------------------------------------------------
CREATE OR ALTER FUNCTION dbo.DecodePercentEncodedUtf8(@s nvarchar(max))
RETURNS nvarchar(max)
AS
BEGIN
    IF @s IS NULL OR @s NOT LIKE '%[%][0-9a-fA-F][0-9a-fA-F]%' RETURN @s;
    DECLARE @out nvarchar(max) = N'', @i int = 1, @len int = LEN(@s);
    DECLARE @b1 int, @b2 int, @b3 int;
    WHILE @i <= @len
    BEGIN
        IF SUBSTRING(@s, @i, 1) = '%' AND @i + 2 <= @len
           AND SUBSTRING(@s, @i + 1, 1) LIKE '[0-9a-fA-F]' AND SUBSTRING(@s, @i + 2, 1) LIKE '[0-9a-fA-F]'
        BEGIN
            SET @b1 = CONVERT(int, CONVERT(varbinary(1), '0x' + SUBSTRING(@s, @i + 1, 2), 1));
            IF @b1 < 0x80  -- ASCII byte
            BEGIN
                SET @out += NCHAR(@b1); SET @i += 3;
            END
            ELSE IF @b1 BETWEEN 0xC2 AND 0xDF AND @i + 5 <= @len AND SUBSTRING(@s, @i + 3, 1) = '%'  -- 2-byte UTF-8 (Hebrew = 0xD7xx)
            BEGIN
                SET @b2 = CONVERT(int, CONVERT(varbinary(1), '0x' + SUBSTRING(@s, @i + 4, 2), 1));
                SET @out += NCHAR((@b1 - 0xC0) * 64 + (@b2 - 0x80)); SET @i += 6;
            END
            ELSE IF @b1 BETWEEN 0xE0 AND 0xEF AND @i + 8 <= @len AND SUBSTRING(@s, @i + 3, 1) = '%' AND SUBSTRING(@s, @i + 6, 1) = '%'  -- 3-byte UTF-8
            BEGIN
                SET @b2 = CONVERT(int, CONVERT(varbinary(1), '0x' + SUBSTRING(@s, @i + 4, 2), 1));
                SET @b3 = CONVERT(int, CONVERT(varbinary(1), '0x' + SUBSTRING(@s, @i + 7, 2), 1));
                SET @out += NCHAR((@b1 - 0xE0) * 4096 + (@b2 - 0x80) * 64 + (@b3 - 0x80)); SET @i += 9;
            END
            ELSE  -- malformed sequence: keep the '%' literally
            BEGIN
                SET @out += SUBSTRING(@s, @i, 1); SET @i += 1;
            END
        END
        ELSE
        BEGIN
            SET @out += SUBSTRING(@s, @i, 1); SET @i += 1;
        END
    END
    RETURN @out;
END
GO

------------------------------------------------------------------------------------------------
-- 1. Preview: what will change (encoded value → decoded value)
------------------------------------------------------------------------------------------------
SELECT 'ProductVariantOptionValue' AS Tbl, ov.ProductVariantId AS Id, p.AccountId,
       ov.OptionValue AS Old, dbo.DecodePercentEncodedUtf8(ov.OptionValue) AS New
FROM ProductVariantOptionValue ov
JOIN ProductVariant v ON v.Id = ov.ProductVariantId
JOIN Product p ON p.Id = v.ProductId
WHERE ov.OptionValue LIKE '%[%]d7[%]%';

SELECT 'OrderItem' AS Tbl, oi.Id, o.AccountId,
       oi.OrderLineCuttingLabel AS OldCut,  dbo.DecodePercentEncodedUtf8(oi.OrderLineCuttingLabel) AS NewCut,
       oi.OrderLineSizeLabel    AS OldSize, dbo.DecodePercentEncodedUtf8(oi.OrderLineSizeLabel)    AS NewSize,
       oi.VariantTitle          AS OldVar,  dbo.DecodePercentEncodedUtf8(oi.VariantTitle)          AS NewVar,
       JSON_VALUE(oi.LineDisplayJson, '$.cuttingName') AS OldJsonCut,
       dbo.DecodePercentEncodedUtf8(JSON_VALUE(oi.LineDisplayJson, '$.cuttingName')) AS NewJsonCut
FROM OrderItem oi
JOIN [Order] o ON o.Id = oi.OrderId
WHERE oi.OrderLineCuttingLabel LIKE '%[%]d7[%]%'
   OR oi.OrderLineSizeLabel    LIKE '%[%]d7[%]%'
   OR oi.VariantTitle          LIKE '%[%]d7[%]%'
   OR oi.LineDisplayJson       LIKE '%[%]d7[%]%';

------------------------------------------------------------------------------------------------
-- 2. THE FIX (compare counts against the preview, then COMMIT - do not leave the transaction open)
------------------------------------------------------------------------------------------------
BEGIN TRAN;

UPDATE ov
SET ov.OptionValue = dbo.DecodePercentEncodedUtf8(ov.OptionValue)
FROM ProductVariantOptionValue ov
WHERE ov.OptionValue LIKE '%[%]d7[%]%';
-- expected: catalog row count from preview

UPDATE oi
SET oi.OrderLineCuttingLabel = dbo.DecodePercentEncodedUtf8(oi.OrderLineCuttingLabel)
FROM OrderItem oi
WHERE oi.OrderLineCuttingLabel LIKE '%[%]d7[%]%';

UPDATE oi
SET oi.OrderLineSizeLabel = dbo.DecodePercentEncodedUtf8(oi.OrderLineSizeLabel)
FROM OrderItem oi
WHERE oi.OrderLineSizeLabel LIKE '%[%]d7[%]%';

UPDATE oi
SET oi.VariantTitle = dbo.DecodePercentEncodedUtf8(oi.VariantTitle)
FROM OrderItem oi
WHERE oi.VariantTitle LIKE '%[%]d7[%]%';

UPDATE oi
SET oi.LineDisplayJson = JSON_MODIFY(oi.LineDisplayJson, '$.cuttingName',
        dbo.DecodePercentEncodedUtf8(JSON_VALUE(oi.LineDisplayJson, '$.cuttingName')))
FROM OrderItem oi
WHERE JSON_VALUE(oi.LineDisplayJson, '$.cuttingName') LIKE '%[%]d7[%]%';

UPDATE oi
SET oi.LineDisplayJson = JSON_MODIFY(oi.LineDisplayJson, '$.sizeName',
        dbo.DecodePercentEncodedUtf8(JSON_VALUE(oi.LineDisplayJson, '$.sizeName')))
FROM OrderItem oi
WHERE JSON_VALUE(oi.LineDisplayJson, '$.sizeName') LIKE '%[%]d7[%]%';

-- ROLLBACK TRAN;
-- COMMIT TRAN;

------------------------------------------------------------------------------------------------
-- 3. Cleanup (after COMMIT)
------------------------------------------------------------------------------------------------
-- DROP FUNCTION dbo.DecodePercentEncodedUtf8;
