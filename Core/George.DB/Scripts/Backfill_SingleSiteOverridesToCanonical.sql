-- =============================================================================
-- Backfill canonical Product rows from per-site overrides — SINGLE-SITE products
-- of NON-network accounts only.
--
-- Why: the frontend sends editScope='selected_site' for every save on a
-- "separate"-mode account, so real values (price, stock, merchandising, images)
-- landed in ProductSiteOverride / ProductSiteImage / ProductSiteCategory /
-- ProductSiteVariantStock while the canonical Product row kept stale values
-- (e.g. Price = 0 from creation). The app and the Woo sync now both resolve
-- overrides, but any consumer reading Product directly still sees the stale
-- canonical values. This script copies the override values onto the canonical
-- row for products where that is unambiguous:
--   * the product is linked to exactly ONE site, and
--   * the owning account is NOT network-managed
--     (ManagementMode <> 'network', and not NULL-mode with an all_sites wizard).
--
-- Override rows are intentionally KEPT (not deleted): the read side and the Woo
-- sync overlay them anyway, and after this script their values equal the
-- canonical ones. Deleting them would change nothing visible but would lose the
-- audit trail; a future selected_site save recreates them regardless.
--
-- Idempotent — safe to re-run.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

-- -----------------------------------------------------------------------------
-- 0. Scope: (product, site, override) triples eligible for backfill.
-- -----------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#Scope') IS NOT NULL DROP TABLE #Scope;

SELECT p.Id AS ProductId, ss.SiteId
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
  AND NOT (                                    -- exclude network-managed accounts
        ISNULL(a.ManagementMode, '') = 'network'
        OR (a.ManagementMode IS NULL AND ISNULL(wt.Name, '') = 'all_sites')
      );

DECLARE @ScopeCount int, @Rows int;
SELECT @ScopeCount = COUNT(*) FROM #Scope;
PRINT CONCAT('Scope: ', @ScopeCount, ' single-site products on non-network accounts.');

-- -----------------------------------------------------------------------------
-- 1. Product-level override values -> canonical Product row.
--    Field semantics mirror ProductService.ApplyEffectiveSiteValuesAsync:
--    nullable override value NULL = "no override, keep canonical";
--    string overrides apply only when non-empty (except descriptions/SEO desc,
--    which apply when non-NULL).
-- -----------------------------------------------------------------------------
UPDATE p SET
    p.Price                    = COALESCE(o.Price, p.Price),
    p.SalePrice                = COALESCE(o.SalePrice, p.SalePrice),
    p.SalePriceStartDate       = COALESCE(o.SalePriceStartDate, p.SalePriceStartDate),
    p.SalePriceEndDate         = COALESCE(o.SalePriceEndDate, p.SalePriceEndDate),
    p.CostPrice                = COALESCE(o.CostPrice, p.CostPrice),
    p.StockManagementTypeId    = COALESCE(o.StockManagementTypeId, p.StockManagementTypeId),
    p.StockStatusId            = COALESCE(o.StockStatusId, p.StockStatusId),
    p.StockQuantity            = COALESCE(o.StockQuantity, p.StockQuantity),
    p.VariationStockByQuantity = COALESCE(o.VariationStockByQuantity, p.VariationStockByQuantity),
    p.LowStockThreshold        = COALESCE(o.LowStockThreshold, p.LowStockThreshold),
    p.Name                     = CASE WHEN NULLIF(LTRIM(RTRIM(o.Name)), '') IS NULL THEN p.Name ELSE o.Name END,
    p.ShortDescription         = COALESCE(o.ShortDescription, p.ShortDescription),
    p.LongDescription          = COALESCE(o.LongDescription, p.LongDescription),
    p.Weight                   = COALESCE(o.Weight, p.Weight),
    p.WeightUnit               = CASE WHEN NULLIF(LTRIM(RTRIM(o.WeightUnit)), '') IS NULL THEN p.WeightUnit ELSE o.WeightUnit END,
    p.Sku                      = CASE WHEN NULLIF(LTRIM(RTRIM(o.Sku)), '') IS NULL THEN p.Sku ELSE o.Sku END,
    p.SeoTitle                 = CASE WHEN NULLIF(LTRIM(RTRIM(o.SeoTitle)), '') IS NULL THEN p.SeoTitle ELSE o.SeoTitle END,
    p.SeoDescription           = COALESCE(o.SeoDescription, p.SeoDescription),
    p.IsKosher                 = COALESCE(o.IsKosher, p.IsKosher),
    p.StatusId                 = COALESCE(o.StatusId, p.StatusId),
    p.VisibilityId             = COALESCE(o.VisibilityId, p.VisibilityId),
    p.Slug                     = CASE WHEN NULLIF(LTRIM(RTRIM(o.Slug)), '') IS NULL THEN p.Slug ELSE o.Slug END,
    p.ShippingClassId          = COALESCE(o.ShippingClassId, p.ShippingClassId),
    p.SupplierId               = COALESCE(o.SupplierId, p.SupplierId),
    p.LabelFrozen              = COALESCE(o.LabelFrozen, p.LabelFrozen),
    p.LabelGlutenFree          = COALESCE(o.LabelGlutenFree, p.LabelGlutenFree),
    p.LabelNotKosher           = COALESCE(o.LabelNotKosher, p.LabelNotKosher),
    p.LabelBestseller          = COALESCE(o.LabelBestseller, p.LabelBestseller),
    p.LabelLowAvailability     = COALESCE(o.LabelLowAvailability, p.LabelLowAvailability),
    p.LabelReadyToCook         = COALESCE(o.LabelReadyToCook, p.LabelReadyToCook),
    p.LabelNatural             = COALESCE(o.LabelNatural, p.LabelNatural),
    p.LabelSugarFree           = COALESCE(o.LabelSugarFree, p.LabelSugarFree),
    p.LabelLactoseFree         = COALESCE(o.LabelLactoseFree, p.LabelLactoseFree),
    p.LabelKosherForPassover   = COALESCE(o.LabelKosherForPassover, p.LabelKosherForPassover),
    p.LabelKosherForPassoverEndDate =
        CASE WHEN o.LabelKosherForPassover IS NULL THEN p.LabelKosherForPassoverEndDate
             WHEN o.LabelKosherForPassover = 1     THEN o.LabelKosherForPassoverEndDate
             ELSE NULL END,
    p.LabelNew                 = COALESCE(o.LabelNew, p.LabelNew),
    p.LabelNewEndDate          =
        CASE WHEN o.LabelNew IS NULL THEN p.LabelNewEndDate
             WHEN o.LabelNew = 1     THEN o.LabelNewEndDate
             ELSE NULL END,
    p.UpdatedDate              = GETUTCDATE()
FROM dbo.Product p
JOIN #Scope sc ON sc.ProductId = p.Id
JOIN dbo.ProductSiteOverride o ON o.ProductId = sc.ProductId AND o.SiteId = sc.SiteId AND o.IsDeleted = 0;

SET @Rows = @@ROWCOUNT;
PRINT CONCAT('Product rows backfilled from ProductSiteOverride: ', @Rows);

-- -----------------------------------------------------------------------------
-- 2. Variant-level override values -> canonical ProductVariant rows.
--    (ProductVariant has no StockStatusId; excluded variants are left as-is —
--    the read side and sync keep hiding them via the override.)
-- -----------------------------------------------------------------------------
UPDATE v SET
    v.Price         = COALESCE(vs.Price, v.Price),
    v.SalePrice     = COALESCE(vs.SalePrice, v.SalePrice),
    v.StockQuantity = COALESCE(vs.StockQuantity, v.StockQuantity)
FROM dbo.ProductVariant v
JOIN dbo.ProductSiteVariantStock vs ON vs.ProductVariantId = v.Id AND vs.IsDeleted = 0 AND vs.IsExcluded = 0
JOIN #Scope sc ON sc.ProductId = v.ProductId AND sc.SiteId = vs.SiteId
WHERE v.IsDeleted = 0;

SET @Rows = @@ROWCOUNT;
PRINT CONCAT('ProductVariant rows backfilled from ProductSiteVariantStock: ', @Rows);

-- -----------------------------------------------------------------------------
-- 3. Per-site images -> canonical ProductImage, ONLY for products that have no
--    canonical images at all (never merges/overwrites an existing gallery).
-- -----------------------------------------------------------------------------
INSERT INTO dbo.ProductImage (ProductId, SortOrder, Url)
SELECT si.ProductId, si.SortOrder, si.Url
FROM dbo.ProductSiteImage si
JOIN #Scope sc ON sc.ProductId = si.ProductId AND sc.SiteId = si.SiteId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductImage pi WHERE pi.ProductId = si.ProductId);

SET @Rows = @@ROWCOUNT;
PRINT CONCAT('ProductImage rows created from ProductSiteImage: ', @Rows);

-- -----------------------------------------------------------------------------
-- 4. Per-site categories -> canonical ProductCategory, ONLY for products that
--    have no canonical category links at all.
-- -----------------------------------------------------------------------------
INSERT INTO dbo.ProductCategory (ProductId, CategoryId, IsPrimary)
SELECT psc.ProductId, psc.CategoryId, 0
FROM dbo.ProductSiteCategory psc
JOIN #Scope sc ON sc.ProductId = psc.ProductId AND sc.SiteId = psc.SiteId
WHERE NOT EXISTS (SELECT 1 FROM dbo.ProductCategory pc WHERE pc.ProductId = psc.ProductId);

SET @Rows = @@ROWCOUNT;
PRINT CONCAT('ProductCategory rows created from ProductSiteCategory: ', @Rows);

COMMIT TRAN;
PRINT 'Backfill committed.';

-- Verification example (Abu Dagesh product 14719):
--   SELECT Id, Price, StockStatusId, IsKosher FROM dbo.Product WHERE Id = 14719;  -- expect Price = 62.00
--   SELECT * FROM dbo.ProductImage WHERE ProductId = 14719;                        -- expect the per-site image URL
