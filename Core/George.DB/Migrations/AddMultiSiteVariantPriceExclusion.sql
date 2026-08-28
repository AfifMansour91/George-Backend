-- Migration: MultiSite Phase 2 - per-site variant price + exclusion.
-- Extends ProductSiteVariantStock (already per-(variant,site) stock) with per-site variant Price/SalePrice and
-- an IsExcluded flag, so a network product's variations can be priced per branch and a variation can be
-- "removed" in one branch (hidden there) without deleting it from the canonical product. Idempotent.

IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'Price') IS NULL
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [Price] decimal(18, 2) NULL;
GO
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'SalePrice') IS NULL
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [SalePrice] decimal(18, 2) NULL;
GO
IF COL_LENGTH(N'[dbo].[ProductSiteVariantStock]', N'IsExcluded') IS NULL
    ALTER TABLE [dbo].[ProductSiteVariantStock] ADD [IsExcluded] bit NOT NULL DEFAULT 0;
GO
