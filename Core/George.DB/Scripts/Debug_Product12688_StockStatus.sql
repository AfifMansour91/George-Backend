-- Diagnostic (read-only): product 12688 (שורט ריב) marked out-of-stock in George but Woo unchanged.
-- Shows which stock mechanism applies and where the out-of-stock value actually lives.

DECLARE @ProductId INT = 12688;

-- 1. Canonical stock config: StockStatus/Status drive the pushed status; StockManagementType =
--    'variation' + VariationStockByQuantity = per-variation quantity tracking (the buggy path).
SELECT p.Id, p.Name, p.AccountId, p.VariationStockByQuantity,
       ss.Name  AS StockStatus,
       ps.Name  AS ProductStatus,
       smt.Name AS StockManagementType,
       st.Name  AS SetupType
FROM Product p
LEFT JOIN StockStatus ss  ON ss.Id  = p.StockStatusId
LEFT JOIN ProductStatus ps ON ps.Id = p.StatusId
LEFT JOIN StockManagementType smt ON smt.Id = p.StockManagementTypeId
LEFT JOIN SetupType st ON st.Id = p.SetupTypeId
WHERE p.Id = @ProductId;

-- 2. Live variants + quantities (qty > 0 here + quantity tracking = variations stayed instock in Woo)
SELECT pv.Id AS VariantId, pv.Sku, pv.StockQuantity, pv.WooCommerceVariationId
FROM ProductVariant pv
WHERE pv.ProductId = @ProductId AND pv.IsDeleted = 0;

-- 3. Per-site override rows (out-of-stock set per branch lands here, not on the canonical product)
SELECT SiteId, IsExcluded, StockStatus, StockQuantity, StockManagementType, UpdatedDate
FROM ProductSiteOverride
WHERE ProductId = @ProductId AND IsDeleted = 0;
