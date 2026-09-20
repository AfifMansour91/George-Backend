-- Order line variation name (PEPE, 2026-09-20): a line of a NON-size attribute ("חלוקה למגשים" = "2") showed
-- the bare value "2" on the order cards and printouts. New lines store the attribute name in
-- OrderItem.LineDisplayJson ($.sizeOptionName) and render "חלוקה למגשים: 2"; this back-fills the name into the
-- existing lines of the last 60 days (older orders are closed - nobody prints them).
--   - only lines whose snapshot has a sizeName and no sizeOptionName yet
--   - name = the variant's option whose value IS that sizeName, excluding size (גודל / size) and cutting options
-- No schema change - run any time after the backend deploy. Safe to re-run.
-- Hebrew words are built from code points so the script doesn't depend on the file encoding.

DECLARE @SizeHe NVARCHAR(10) = NCHAR(0x05D2) + NCHAR(0x05D5) + NCHAR(0x05D3) + NCHAR(0x05DC);              -- גודל
DECLARE @CutHe  NVARCHAR(10) = NCHAR(0x05D7) + NCHAR(0x05D9) + NCHAR(0x05EA) + NCHAR(0x05D5) + NCHAR(0x05DA); -- חיתוך

;WITH target AS (
    SELECT oi.Id,
           (SELECT MIN(ov.OptionName)
              FROM [dbo].[ProductVariantOptionValue] ov
             WHERE ov.ProductVariantId = oi.ProductVariantId
               AND LTRIM(RTRIM(ov.OptionValue)) = LTRIM(RTRIM(JSON_VALUE(oi.LineDisplayJson, '$.sizeName')))
               AND LTRIM(RTRIM(ov.OptionName)) NOT IN (@SizeHe, N'size')
               AND ov.OptionName NOT LIKE N'%' + @CutHe + N'%'
               AND ov.OptionName NOT LIKE N'%cutting%') AS OptionName
    FROM [dbo].[OrderItem] oi
    JOIN [dbo].[Order] o ON o.Id = oi.OrderId
    WHERE o.CreationTime >= DATEADD(DAY, -60, SYSUTCDATETIME())
      AND oi.ProductVariantId IS NOT NULL
      AND ISJSON(oi.LineDisplayJson) = 1
      AND JSON_VALUE(oi.LineDisplayJson, '$.sizeName') IS NOT NULL
      AND JSON_VALUE(oi.LineDisplayJson, '$.sizeOptionName') IS NULL
      -- a variant that also has a real size option: sizeName is that size, nothing to name
      AND NOT EXISTS (SELECT 1 FROM [dbo].[ProductVariantOptionValue] sz
                       WHERE sz.ProductVariantId = oi.ProductVariantId
                         AND LTRIM(RTRIM(sz.OptionName)) IN (@SizeHe, N'size'))
)
UPDATE oi
   SET oi.LineDisplayJson = JSON_MODIFY(oi.LineDisplayJson, '$.sizeOptionName', LTRIM(RTRIM(t.OptionName)))
FROM [dbo].[OrderItem] oi
JOIN target t ON t.Id = oi.Id
WHERE t.OptionName IS NOT NULL;
PRINT CONCAT('Order lines named: ', @@ROWCOUNT);
GO
