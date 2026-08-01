-- Order #171 (Id 4538, ExternalOrderId 4192, ג'ורג' שופאני) was picked while the backend still
-- treated the unlinked promo stamp as fixed, so the line pair is broken:
--   OrderItem 8849 (נתח סינטה): TotalPrice = 136.95 (picked 0.55kg) but DiscountAmount = 6.23
--   (the intake stamp for the ordered 124.50 gross). The paired value is 6.85.
-- The new code keeps the pair in sync on every picking save, but it scales from the CURRENT pair,
-- so this one order must be repaired once. Run on Prod after deploying the backend.

UPDATE dbo.OrderItem
SET DiscountAmount = 6.85
WHERE Id = 8849 AND OrderId = 4538 AND DiscountAmount = 6.23;

-- Header: SubTotal 234.95 (136.95 + 98.00) − discounts (6.85 + 4.90), free shipping.
UPDATE dbo.[Order]
SET Total = 223.20
WHERE Id = 4538 AND ExternalOrderId = '4192';
