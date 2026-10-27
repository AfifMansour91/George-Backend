-- Deliz (site 22) 2026-10-28: ProductSiteVariantWooId rows for the site point at OLD-store variation ids (2xxx) while
-- the live store's ids (3xxx) are in the legacy ProductVariant.WooCommerceVariationId column (the product sync keeps
-- only the legacy column up to date on non-network-managed sites). The bundle push preferred the per-site rows →
-- bundle 15309 failed since 21/9 with "oc_bundles_invalid_component: Component product 2319 does not exist".
-- The code now prefers the legacy column on non-network sites; this script aligns the stale rows so nothing else
-- (incoming Woo order variation lookup) trips over them. Preview → BEGIN TRAN → counts → COMMIT manually.

-- Preview (expected ~195 rows on site 22)
SELECT m.ProductVariantId, m.ProductId, m.WooCommerceVariationId AS perSite, v.WooCommerceVariationId AS legacy
FROM dbo.ProductSiteVariantWooId m JOIN dbo.ProductVariant v ON v.Id = m.ProductVariantId
WHERE m.SiteId = 22 AND v.WooCommerceVariationId IS NOT NULL AND v.WooCommerceVariationId <> m.WooCommerceVariationId;

BEGIN TRAN;
UPDATE m SET m.WooCommerceVariationId = v.WooCommerceVariationId
FROM dbo.ProductSiteVariantWooId m JOIN dbo.ProductVariant v ON v.Id = m.ProductVariantId
WHERE m.SiteId = 22 AND v.WooCommerceVariationId IS NOT NULL AND v.WooCommerceVariationId <> m.WooCommerceVariationId;
PRINT CONCAT('Aligned per-site variation ids on site 22: ', @@ROWCOUNT);
-- COMMIT;   -- after checking the count
-- ROLLBACK;
