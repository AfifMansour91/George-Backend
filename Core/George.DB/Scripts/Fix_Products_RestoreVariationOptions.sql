-- Products whose variants still carry option values (ProductVariantOptionValue) while EVERY ProductOption
-- row is soft-deleted or missing. The WooCommerce sync builds the variable product's attributes from
-- active ProductOption rows only, so these products reach the store as "variable" with no attributes and
-- no variations - Woo shows them out of stock whatever stock is pushed.
-- Meshek Basar PT 2026-09-09: 12726 "צלעות טלה" (3 deleted generations of "צורת חיתוך"), also 11887 (account 42)
-- and 13500 (account 46) with no option rows at all.
-- Repair: per product and option name used by its active variants, un-delete the newest matching option row
-- (or create one) and make sure it lists every value the variants use.
-- Afterwards: save each product in George (or run a site sync) so the store gets the attributes + variations.
--
-- Run block 1 (preview), then block 2 inside the transaction, check the counts, then COMMIT manually.

-- ===== 1. Preview =====
;WITH needed AS (
    SELECT DISTINCT v.ProductId, ov.OptionName, ov.OptionValue
    FROM dbo.ProductVariant v
    JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
    JOIN dbo.Product p ON p.Id = v.ProductId AND p.IsDeleted = 0
    WHERE v.IsDeleted = 0
      AND NOT EXISTS (SELECT 1 FROM dbo.ProductOption o WHERE o.ProductId = v.ProductId AND o.IsDeleted = 0)
)
SELECT n.ProductId, p.AccountId, p.Name AS Product, n.OptionName,
    STRING_AGG(n.OptionValue, ' | ') AS ValuesFromVariants,
    (SELECT MAX(o.Id) FROM dbo.ProductOption o WHERE o.ProductId = n.ProductId AND o.Name = n.OptionName) AS ReusableDeletedOptionId
FROM needed n JOIN dbo.Product p ON p.Id = n.ProductId
GROUP BY n.ProductId, p.AccountId, p.Name, n.OptionName
ORDER BY n.ProductId;
-- expect (2026-09-09): 11887 צורת-חיתוך (4 values, no row) / 12726 צורת חיתוך (2 values, reuse 8504) / 13500 trays (10 values, no row)

-- ===== 2. Apply (COMMIT manually after checking the counts) =====
BEGIN TRAN;

DECLARE @needed TABLE (ProductId INT, OptionName NVARCHAR(200), OptionValue NVARCHAR(400));
INSERT @needed
SELECT DISTINCT v.ProductId, ov.OptionName, ov.OptionValue
FROM dbo.ProductVariant v
JOIN dbo.ProductVariantOptionValue ov ON ov.ProductVariantId = v.Id
JOIN dbo.Product p ON p.Id = v.ProductId AND p.IsDeleted = 0
WHERE v.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductOption o WHERE o.ProductId = v.ProductId AND o.IsDeleted = 0);

-- 2a. Reuse the newest soft-deleted row per (product, option name).
UPDATE o SET o.IsDeleted = 0
FROM dbo.ProductOption o
WHERE o.Id IN (
    SELECT MAX(o2.Id) FROM dbo.ProductOption o2
    JOIN (SELECT DISTINCT ProductId, OptionName FROM @needed) n ON n.ProductId = o2.ProductId AND n.OptionName = o2.Name
    GROUP BY o2.ProductId, o2.Name);
SELECT @@ROWCOUNT AS options_undeleted;        -- expect 1 (12726 / 8504)

-- 2b. Create the option rows that never existed.
INSERT INTO dbo.ProductOption (ProductId, Name, IsDeleted)
SELECT DISTINCT n.ProductId, n.OptionName, 0
FROM @needed n
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductOption o WHERE o.ProductId = n.ProductId AND o.Name = n.OptionName AND o.IsDeleted = 0);
SELECT @@ROWCOUNT AS options_created;          -- expect 2 (11887, 13500)

-- 2c. Every value the variants use must be listed on the (now active) option.
INSERT INTO dbo.ProductOptionValue (ProductOptionId, Value)
SELECT o.Id, n.OptionValue
FROM @needed n
JOIN dbo.ProductOption o ON o.ProductId = n.ProductId AND o.Name = n.OptionName AND o.IsDeleted = 0
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductOptionValue pv WHERE pv.ProductOptionId = o.Id AND pv.Value = n.OptionValue);
SELECT @@ROWCOUNT AS values_added;             -- expect 14 (4 + 0 + 10)

-- Verify: no product left with variant option values and no active option.
SELECT v.ProductId, COUNT(*) AS variants
FROM dbo.ProductVariant v
WHERE v.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM dbo.ProductVariantOptionValue ov WHERE ov.ProductVariantId = v.Id)
  AND NOT EXISTS (SELECT 1 FROM dbo.ProductOption o WHERE o.ProductId = v.ProductId AND o.IsDeleted = 0)
GROUP BY v.ProductId;                          -- expect no rows

SELECT o.Id, o.ProductId, o.Name, o.IsDeleted, (SELECT STRING_AGG(pv.Value, ' | ') FROM dbo.ProductOptionValue pv WHERE pv.ProductOptionId = o.Id) AS Vals
FROM dbo.ProductOption o WHERE o.ProductId IN (11887, 12726, 13500) AND o.IsDeleted = 0;

-- ROLLBACK;
-- COMMIT;
