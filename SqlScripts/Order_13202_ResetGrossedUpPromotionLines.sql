-- הקצב בעיר (site 42) order 13202 / #9 (Woo #3398?, created 2026-10-11 18:04, still New/Unpaid).
-- Ingested with 4 promotions and empty lines[] → every line was grossed up by the order-level fallback
-- (PricePerUnit ×1.04, e.g. 85 → 88.38) and stamped with the last promotion (28). At picking the evaluator
-- re-derives discounts per product, so lines promotion 28 does not target would be charged at the inflated
-- ₪/kg (same mechanism as order 9013 / #7, ₪23.11 overcharged).
-- Reset the lines to what the store charged: PricePerUnit = payload unitPrice (net), TotalPrice = payload
-- lineTotal, no stamp. Picking then trusts Woo (no George-linked promotion → no re-evaluation) and charges
-- picked kg × the net rate the customer saw. Order totals already equal the payload (OriginalTotal 1246.90).
--
-- 1) PREVIEW
SELECT oi.Id, oi.Title, oi.Quantity, oi.PricePerUnit AS CurrentPPU, oi.TotalPrice AS CurrentTotal,
       oi.DiscountAmount AS CurrentDisc, oi.PromotionId AS CurrentPromo,
       p.unitPrice AS NewPPU, p.lineTotal AS NewTotal
FROM OrderItem oi
JOIN [Order] o ON o.Id = oi.OrderId
CROSS APPLY OPENJSON(o.WooCommerceRequestJson, '$.items')
     WITH (productId int '$.productId', unitPrice decimal(10,2) '$.unitPrice', lineTotal decimal(10,2) '$.lineTotal') p
WHERE o.Id = 13202 AND oi.IsDeleted = 0 AND p.productId = oi.WooCommerceProductId
ORDER BY oi.Id;

-- 2) APPLY (run after the preview looks right; the order must still be New - re-check before COMMIT)
BEGIN TRAN;
UPDATE oi
SET oi.PricePerUnit = p.unitPrice,
    oi.TotalPrice = p.lineTotal,
    oi.DiscountAmount = NULL,
    oi.PromotionId = NULL,
    oi.UpdatedDate = GETUTCDATE()
FROM OrderItem oi
JOIN [Order] o ON o.Id = oi.OrderId
CROSS APPLY OPENJSON(o.WooCommerceRequestJson, '$.items')
     WITH (productId int '$.productId', unitPrice decimal(10,2) '$.unitPrice', lineTotal decimal(10,2) '$.lineTotal') p
WHERE o.Id = 13202 AND oi.IsDeleted = 0 AND p.productId = oi.WooCommerceProductId
  AND o.Status = 'New' AND o.PaymentStatus <> 'Paid';

SELECT @@ROWCOUNT AS UpdatedLines; -- expect 13 (one row per line; product 3328 appears on two lines)
SELECT oi.Id, oi.PricePerUnit, oi.TotalPrice, oi.DiscountAmount, oi.PromotionId FROM OrderItem oi WHERE oi.OrderId = 13202 AND oi.IsDeleted = 0;
-- COMMIT;   -- or ROLLBACK;
