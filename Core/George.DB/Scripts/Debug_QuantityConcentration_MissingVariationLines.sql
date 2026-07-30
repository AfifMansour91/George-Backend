-- Diagnostic (read-only) for: דוח ריכוז כמויות not showing variation/option detail rows
-- for products that DO have catalog variations (e.g. "אנטריקוט על עצם"), customer GDBEEF.
--
-- The report builds detail rows ONLY from what is stored on each OrderItem:
--   VariantTitle -> OrderLineCuttingLabel -> OrderLineSizeLabel (first non-empty, in that order).
-- A label is DISCARDED (treated as "not an option") when it is generic ("יחידה", "ק\"ג")
-- or weight/quantity-like ("500 גרם", "1 ק\"ג", "2 יח'...").
-- If no line of a product resolves a label, the product shows only its parent row — no variations.
--
-- Run each section and look at the "diagnosis" column.
-- Set these two parameters first:

DECLARE @SiteId INT = 0;                              -- TODO: GDBEEF site id
DECLARE @ProductName NVARCHAR(200) = N'אנטריקוט על עצם';

-- ============================================================================
-- 1) Catalog side: does the product have active variations, and what are their names?
-- ============================================================================
SELECT p.Id AS ProductId, p.Name, p.IsWeighted, p.VariationStockByQuantity,
       v.Id AS VariantId, v.IsDeleted, v.WooCommerceVariationId, v.Weight, v.StockQuantity,
       ov.OptionName, ov.OptionValue
FROM dbo.Product p
JOIN dbo.ProductSite ps ON ps.ProductId = p.Id AND ps.SiteId = @SiteId
LEFT JOIN dbo.ProductVariant v ON v.ProductId = p.Id AND v.IsDeleted = 0
LEFT JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
WHERE p.Name LIKE '%' + @ProductName + '%' AND p.IsDeleted = 0
ORDER BY p.Id, v.Id;

-- ============================================================================
-- 2) Order-line side: what do OPEN order lines of this product actually carry?
--    (same population as the report: order not Delivered/Cancelled, not deleted)
-- ============================================================================
SELECT o.Id AS OrderId, o.Source, o.Status,
       COALESCE(o.DeliveryDate, o.PickupDate, o.CreationTime) AS EffectiveDate,
       oi.Id AS OrderItemId, oi.Title,
       oi.VariantTitle, oi.OrderLineCuttingLabel, oi.OrderLineSizeLabel,
       oi.ProductVariantId, oi.WooCommerceVariationId,
       oi.OrderLineQuantityMode, oi.Quantity, oi.Notes,
       CASE
         WHEN oi.VariantTitle IS NULL AND oi.OrderLineCuttingLabel IS NULL AND oi.OrderLineSizeLabel IS NULL
           THEN N'אין שום label על השורה -> לא תוצג שורת וריאציה (בדוק LinePayloadJson למטה: האם הפלאגין שלח variation.attributes?)'
         WHEN LTRIM(RTRIM(ISNULL(oi.VariantTitle, N''))) IN (N'יחידה', N'ק"ג', N'קג', N'kg')
              AND oi.OrderLineCuttingLabel IS NULL AND oi.OrderLineSizeLabel IS NULL
           THEN N'label גנרי ("יחידה"/"ק"ג") -> מסונן בכוונה'
         WHEN oi.VariantTitle LIKE N'[0-9]%'
           THEN N'label מתחיל במספר (למשל "500 גרם") -> מסונן כטקסט כמות, לא כאפשרות'
         ELSE N'יש label תקין -> אמור להופיע; אם לא — בדוק שהמוצר עם וריאציות פעילות בקטלוג'
       END AS diagnosis,
       oi.LinePayloadJson
FROM dbo.[Order] o
JOIN dbo.OrderItem oi ON oi.OrderId = o.Id AND oi.IsDeleted = 0
JOIN dbo.Product p ON p.Id = oi.ProductId
WHERE o.SiteId = @SiteId AND o.IsDeleted = 0
  AND o.Status NOT IN ('Delivered', 'Cancelled')
  AND p.Name LIKE '%' + @ProductName + '%'
ORDER BY o.Id DESC, oi.SortOrder;

-- ============================================================================
-- 3) Site-wide summary: how many open-order lines of variation-products have no usable label,
--    grouped by product — this lists all the "many products" the customer sees.
-- ============================================================================
SELECT p.Id AS ProductId, p.Name,
       COUNT(*) AS OpenLines,
       SUM(CASE WHEN oi.VariantTitle IS NULL AND oi.OrderLineCuttingLabel IS NULL AND oi.OrderLineSizeLabel IS NULL
                THEN 1 ELSE 0 END) AS LinesWithNoLabel,
       SUM(CASE WHEN oi.VariantTitle LIKE N'[0-9]%' THEN 1 ELSE 0 END) AS LinesWithNumericWeightLabel,
       SUM(CASE WHEN oi.ProductVariantId IS NULL THEN 1 ELSE 0 END) AS LinesNotLinkedToVariant,
       MAX(o.Source) AS SampleSource
FROM dbo.[Order] o
JOIN dbo.OrderItem oi ON oi.OrderId = o.Id AND oi.IsDeleted = 0
JOIN dbo.Product p ON p.Id = oi.ProductId
WHERE o.SiteId = @SiteId AND o.IsDeleted = 0
  AND o.Status NOT IN ('Delivered', 'Cancelled')
  AND EXISTS (SELECT 1 FROM dbo.ProductVariant v WHERE v.ProductId = p.Id AND v.IsDeleted = 0)
GROUP BY p.Id, p.Name
HAVING SUM(CASE WHEN oi.VariantTitle IS NULL AND oi.OrderLineCuttingLabel IS NULL AND oi.OrderLineSizeLabel IS NULL
                THEN 1 ELSE 0 END) > 0
    OR SUM(CASE WHEN oi.VariantTitle LIKE N'[0-9]%' THEN 1 ELSE 0 END) > 0
ORDER BY LinesWithNoLabel DESC, p.Name;
