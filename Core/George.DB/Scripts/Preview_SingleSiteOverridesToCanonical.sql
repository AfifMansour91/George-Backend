-- =============================================================================
-- PREVIEW (read-only) for Backfill_SingleSiteOverridesToCanonical.sql
-- Shows exactly which products/variants/images/categories the backfill would
-- touch and the before/after values. Runs NO updates — safe on Prod anytime.
-- Same scope logic as the real script: single-site products of non-network accounts.
-- =============================================================================

SET NOCOUNT ON;

IF OBJECT_ID('tempdb..#Scope') IS NOT NULL DROP TABLE #Scope;

SELECT p.Id AS ProductId, ss.SiteId, s.Name AS SiteName, a.Id AS AccountId
INTO #Scope
FROM dbo.Product p
JOIN (
    SELECT ProductId, MIN(SiteId) AS SiteId
    FROM dbo.ProductSite
    GROUP BY ProductId
    HAVING COUNT(*) = 1                       -- product lives on exactly one site
) ss ON ss.ProductId = p.Id
JOIN dbo.Site s      ON s.Id = ss.SiteId
JOIN dbo.Account a   ON a.Id = s.AccountId
LEFT JOIN dbo.WizardType wt ON wt.Id = a.WizardTypeId
WHERE p.IsDeleted = 0
  AND NOT (
        ISNULL(a.ManagementMode, '') = 'network'
        OR (a.ManagementMode IS NULL AND ISNULL(wt.Name, '') = 'all_sites')
      );

-- -----------------------------------------------------------------------------
-- 1. Products whose canonical row would actually CHANGE, with before -> after
--    for the most important fields. (A row appears only when at least one
--    override value differs from the canonical value.)
-- -----------------------------------------------------------------------------
SELECT
    sc.AccountId,
    sc.SiteId,
    sc.SiteName,
    p.Id                AS ProductId,
    p.Name              AS ProductName,
    p.Sku,
    p.Price             AS Price_Canonical,
    o.Price             AS Price_Override,
    p.SalePrice         AS SalePrice_Canonical,
    o.SalePrice         AS SalePrice_Override,
    p.CostPrice         AS CostPrice_Canonical,
    o.CostPrice         AS CostPrice_Override,
    p.StockStatusId     AS StockStatusId_Canonical,
    o.StockStatusId     AS StockStatusId_Override,
    p.StockManagementTypeId AS StockMgmtTypeId_Canonical,
    o.StockManagementTypeId AS StockMgmtTypeId_Override,
    p.StockQuantity     AS StockQty_Canonical,
    o.StockQuantity     AS StockQty_Override,
    p.StatusId          AS StatusId_Canonical,
    o.StatusId          AS StatusId_Override,
    p.VisibilityId      AS VisibilityId_Canonical,
    o.VisibilityId      AS VisibilityId_Override,
    o.UpdatedDate       AS Override_UpdatedDate
FROM #Scope sc
JOIN dbo.Product p             ON p.Id = sc.ProductId
JOIN dbo.ProductSiteOverride o ON o.ProductId = sc.ProductId AND o.SiteId = sc.SiteId AND o.IsDeleted = 0
WHERE  (o.Price                 IS NOT NULL AND o.Price                 <> p.Price)
    OR (o.SalePrice             IS NOT NULL AND (p.SalePrice IS NULL OR o.SalePrice <> p.SalePrice))
    OR (o.CostPrice             IS NOT NULL AND (p.CostPrice IS NULL OR o.CostPrice <> p.CostPrice))
    OR (o.StockStatusId         IS NOT NULL AND (p.StockStatusId IS NULL OR o.StockStatusId <> p.StockStatusId))
    OR (o.StockManagementTypeId IS NOT NULL AND (p.StockManagementTypeId IS NULL OR o.StockManagementTypeId <> p.StockManagementTypeId))
    OR (o.StockQuantity         IS NOT NULL AND (p.StockQuantity IS NULL OR o.StockQuantity <> p.StockQuantity))
    OR (o.StatusId              IS NOT NULL AND (p.StatusId IS NULL OR o.StatusId <> p.StatusId))
    OR (o.VisibilityId          IS NOT NULL AND (p.VisibilityId IS NULL OR o.VisibilityId <> p.VisibilityId))
    OR (NULLIF(LTRIM(RTRIM(o.Name)), '') IS NOT NULL AND o.Name <> p.Name)
    OR (NULLIF(LTRIM(RTRIM(o.Sku)),  '') IS NOT NULL AND (p.Sku IS NULL OR o.Sku <> p.Sku))
    OR (o.SupplierId            IS NOT NULL AND (p.SupplierId IS NULL OR o.SupplierId <> p.SupplierId))
    OR (o.IsKosher              IS NOT NULL AND o.IsKosher <> p.IsKosher)
ORDER BY sc.AccountId, sc.SiteId, p.Id;

-- -----------------------------------------------------------------------------
-- 2. Variants whose canonical values would change (price/sale/stock).
-- -----------------------------------------------------------------------------
SELECT
    sc.SiteId,
    v.ProductId,
    p.Name              AS ProductName,
    v.Id                AS VariantId,
    v.Sku               AS VariantSku,
    v.Price             AS Price_Canonical,
    vs.Price            AS Price_Override,
    v.SalePrice         AS SalePrice_Canonical,
    vs.SalePrice        AS SalePrice_Override,
    v.StockQuantity     AS StockQty_Canonical,
    vs.StockQuantity    AS StockQty_Override
FROM dbo.ProductVariant v
JOIN dbo.ProductSiteVariantStock vs ON vs.ProductVariantId = v.Id AND vs.IsDeleted = 0 AND vs.IsExcluded = 0
JOIN #Scope sc ON sc.ProductId = v.ProductId AND sc.SiteId = vs.SiteId
JOIN dbo.Product p ON p.Id = v.ProductId
WHERE v.IsDeleted = 0
  AND (   (vs.Price         IS NOT NULL AND (v.Price IS NULL OR vs.Price <> v.Price))
       OR (vs.SalePrice     IS NOT NULL AND (v.SalePrice IS NULL OR vs.SalePrice <> v.SalePrice))
       OR (vs.StockQuantity IS NOT NULL AND (v.StockQuantity IS NULL OR vs.StockQuantity <> v.StockQuantity)) )
ORDER BY v.ProductId, v.Id;

-- -----------------------------------------------------------------------------
-- 3. Images that would be COPIED to canonical (products with per-site images
--    and no canonical gallery at all).
-- -----------------------------------------------------------------------------
SELECT
    sc.SiteId,
    si.ProductId,
    p.Name       AS ProductName,
    si.SortOrder,
    si.Url       AS ImageUrl_WouldBeCopied
FROM dbo.ProductSiteImage si
JOIN #Scope sc ON sc.ProductId = si.ProductId AND sc.SiteId = si.SiteId
JOIN dbo.Product p ON p.Id = si.ProductId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImage pi WHERE pi.ProductId = si.ProductId)
ORDER BY si.ProductId, si.SortOrder;

-- -----------------------------------------------------------------------------
-- 4. Category links that would be CREATED (products with per-site categories
--    and no canonical category links at all).
-- -----------------------------------------------------------------------------
SELECT
    sc.SiteId,
    psc.ProductId,
    p.Name       AS ProductName,
    psc.CategoryId,
    c.Name       AS CategoryName
FROM dbo.ProductSiteCategory psc
JOIN #Scope sc ON sc.ProductId = psc.ProductId AND sc.SiteId = psc.SiteId
JOIN dbo.Product p  ON p.Id = psc.ProductId
JOIN dbo.Category c ON c.Id = psc.CategoryId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategory pc WHERE pc.ProductId = psc.ProductId)
ORDER BY psc.ProductId, psc.CategoryId;

-- -----------------------------------------------------------------------------
-- 5. Summary counts.
-- -----------------------------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM #Scope)                                                          AS ProductsInScope,
    (SELECT COUNT(*) FROM #Scope sc
       JOIN dbo.ProductSiteOverride o ON o.ProductId = sc.ProductId AND o.SiteId = sc.SiteId AND o.IsDeleted = 0
       JOIN dbo.Product p ON p.Id = sc.ProductId
      WHERE o.Price IS NOT NULL AND o.Price <> p.Price)                                    AS ProductsWithPriceChange,
    (SELECT COUNT(DISTINCT si.ProductId) FROM dbo.ProductSiteImage si
       JOIN #Scope sc ON sc.ProductId = si.ProductId AND sc.SiteId = si.SiteId
      WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImage pi WHERE pi.ProductId = si.ProductId)) AS ProductsGettingImages;
