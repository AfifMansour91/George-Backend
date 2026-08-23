-- Order lines pointing at DELETED product variants → relink to the live variant with the same option values.
--
-- Why: saving/syncing a product soft-deletes and recreates all its variants with new ids (see the many
-- IsDeleted=1 copies per Woo variation id). Every order line keeps the OLD ProductVariantId, so the picking
-- catalog lookup by variant id fails and falls back to the product base price — a cutting-surcharge variant
-- (לברק 145 ₪/kg) was priced at 95 ₪/kg (Zano Dagim order 1149, 23/08/2026). Lines that carry a
-- OrderLineCuttingLabel snapshot were mostly unaffected; lines relinked by Fix_ZanoDagim_DuplicateWooProducts
-- have no snapshot. Frontend now also falls back to matching the variant by variantTitle, so after deploy this
-- script is for data hygiene (reports, per-variant stock) rather than strictly required for pricing.
--
-- Scope: @SiteId = NULL → all sites; set a site id to limit. Only lines with exactly ONE live variant whose
-- option-value set equals the deleted variant's are updated.

DECLARE @SiteId int = NULL;   -- e.g. 45 for Zano Dagim

------------------------------------------------------------------------------------------------
-- 0. Preview: per site, how many lines are on deleted variants and how many can be relinked
------------------------------------------------------------------------------------------------
;WITH vkey AS (
    SELECT pv.Id, pv.ProductId, pv.IsDeleted,
           (SELECT STRING_AGG(LOWER(LTRIM(RTRIM(v.OptionName))) + '=' + LOWER(LTRIM(RTRIM(v.OptionValue))), ' ; ')
                   WITHIN GROUP (ORDER BY v.OptionName, v.OptionValue)
            FROM ProductVariantOptionValue v WHERE v.ProductVariantId = pv.Id) AS K
    FROM ProductVariant pv
),
cand AS (
    SELECT oi.Id AS LineId, o.SiteId, dv.ProductId,
           (SELECT COUNT(*) FROM vkey lv WHERE lv.ProductId = dv.ProductId AND lv.IsDeleted = 0 AND lv.K = dv.K AND dv.K IS NOT NULL) AS LiveMatches
    FROM OrderItem oi
    JOIN [Order] o ON o.Id = oi.OrderId AND o.IsDeleted = 0
    JOIN vkey dv ON dv.Id = oi.ProductVariantId AND dv.IsDeleted = 1
    WHERE (@SiteId IS NULL OR o.SiteId = @SiteId)
)
SELECT SiteId, COUNT(*) AS LinesOnDeletedVariant,
       SUM(CASE WHEN LiveMatches = 1 THEN 1 ELSE 0 END) AS WillRelink,
       SUM(CASE WHEN LiveMatches = 0 THEN 1 ELSE 0 END) AS NoLiveMatch,
       SUM(CASE WHEN LiveMatches > 1 THEN 1 ELSE 0 END) AS Ambiguous
FROM cand GROUP BY SiteId ORDER BY LinesOnDeletedVariant DESC;

------------------------------------------------------------------------------------------------
-- 1. THE FIX (check the count against WillRelink, then COMMIT — do not leave the transaction open)
------------------------------------------------------------------------------------------------
BEGIN TRAN;

;WITH vkey AS (
    SELECT pv.Id, pv.ProductId, pv.IsDeleted,
           (SELECT STRING_AGG(LOWER(LTRIM(RTRIM(v.OptionName))) + '=' + LOWER(LTRIM(RTRIM(v.OptionValue))), ' ; ')
                   WITHIN GROUP (ORDER BY v.OptionName, v.OptionValue)
            FROM ProductVariantOptionValue v WHERE v.ProductVariantId = pv.Id) AS K
    FROM ProductVariant pv
),
target AS (
    SELECT oi.Id AS LineId, MIN(lv.Id) AS NewVariantId, COUNT(*) AS N
    FROM OrderItem oi
    JOIN [Order] o ON o.Id = oi.OrderId AND o.IsDeleted = 0
    JOIN vkey dv ON dv.Id = oi.ProductVariantId AND dv.IsDeleted = 1 AND dv.K IS NOT NULL
    JOIN vkey lv ON lv.ProductId = dv.ProductId AND lv.IsDeleted = 0 AND lv.K = dv.K
    WHERE (@SiteId IS NULL OR o.SiteId = @SiteId)
    GROUP BY oi.Id
    HAVING COUNT(*) = 1
)
UPDATE oi SET oi.ProductVariantId = t.NewVariantId
FROM OrderItem oi JOIN target t ON t.LineId = oi.Id;
SELECT @@ROWCOUNT AS LinesRelinked;

-- COMMIT;
-- ROLLBACK;

------------------------------------------------------------------------------------------------
-- 2. Verify: Zano Dagim open-order lines should all point at live variants now
------------------------------------------------------------------------------------------------
SELECT o.OrderNumber, oi.Id, oi.ProductVariantId, pv.IsDeleted, pv.Price
FROM OrderItem oi JOIN [Order] o ON o.Id = oi.OrderId JOIN ProductVariant pv ON pv.Id = oi.ProductVariantId
WHERE o.SiteId = 45 AND o.PaymentStatus = 'Unpaid' AND o.IsDeleted = 0
ORDER BY pv.IsDeleted DESC, o.OrderNumber;

-- FOLLOW-UP (code, not data): ProductStorage should update variants in place (match by option set) instead of
-- delete+recreate on every save — otherwise this drift returns with every product edit.
