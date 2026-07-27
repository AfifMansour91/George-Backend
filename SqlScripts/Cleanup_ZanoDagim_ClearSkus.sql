-- Zano Dagim request (27/07/2026): remove ALL SKUs from products, including variants.
-- Clears Sku in the three places it lives:
--   1. Product.Sku            (canonical)
--   2. ProductVariant.Sku     (variants)
--   3. ProductSiteOverride.Sku (per-site SKU overrides)
-- TemplateProduct (global catalog) is intentionally NOT touched.
-- Idempotent - re-running is a no-op.
--
-- Scoping: products are matched to the account by AccountId OR OwnerSiteId OR a
-- ProductSiteWooId link to one of the account's sites - Product.AccountId is nullable
-- and historically unreliable (same gotcha as Category.AccountId), so AccountId alone
-- would miss rows.
--
-- AFTER RUNNING: trigger a full product sync for the site(s) from the products screen.
-- The sync pushes sku:"" for empty SKUs, which clears them in WooCommerce too
-- (products AND variations). Without a sync, Woo keeps the old SKUs.
--
-- BEFORE RUNNING: set @SiteId to a Zano Dagim Site.Id (the account is derived from it;
-- NOTE: this clears SKUs for the WHOLE account - all its branches share the catalog).
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SiteId INT = NULL;  -- <<<< SET ME: a Zano Dagim Site.Id >>>>

IF @SiteId IS NULL
    THROW 50000, N'Set @SiteId to a Zano Dagim Site.Id before running.', 1;

DECLARE @AccountId INT = (SELECT AccountId FROM [dbo].[Site] WHERE Id = @SiteId);
IF @AccountId IS NULL
    THROW 50000, N'Site not found for @SiteId.', 1;

-- All product ids belonging to the account (see scoping note above).
SELECT DISTINCT p.Id
INTO #Scope
FROM [dbo].[Product] p
WHERE p.AccountId = @AccountId
   OR p.OwnerSiteId IN (SELECT s.Id FROM [dbo].[Site] s WHERE s.AccountId = @AccountId)
   OR EXISTS (SELECT 1
              FROM [dbo].[ProductSiteWooId] w
              JOIN [dbo].[Site] s ON s.Id = w.SiteId
              WHERE w.ProductId = p.Id AND s.AccountId = @AccountId);

DECLARE @ScopeCount INT = (SELECT COUNT(*) FROM #Scope);
PRINT CONCAT(N'Products in scope: ', @ScopeCount);

BEGIN TRAN;

UPDATE p SET p.Sku = NULL
FROM [dbo].[Product] p
JOIN #Scope sc ON sc.Id = p.Id
WHERE p.Sku IS NOT NULL;
PRINT CONCAT(N'Product SKUs cleared: ', @@ROWCOUNT);

UPDATE v SET v.Sku = NULL
FROM [dbo].[ProductVariant] v
JOIN #Scope sc ON sc.Id = v.ProductId
WHERE v.Sku IS NOT NULL;
PRINT CONCAT(N'Variant SKUs cleared: ', @@ROWCOUNT);

UPDATE o SET o.Sku = NULL
FROM [dbo].[ProductSiteOverride] o
JOIN #Scope sc ON sc.Id = o.ProductId
WHERE o.Sku IS NOT NULL;
PRINT CONCAT(N'Per-site override SKUs cleared: ', @@ROWCOUNT);

COMMIT;

DROP TABLE #Scope;
