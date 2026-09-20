-- Bundles (מארזים): an alternative product may carry its OWN quantity per bundle (in the swap product's unit:
-- kg for a by_weight product, units otherwise). NULL = inherit the slot quantity (converted by weight when the
-- swap product is measured differently). Idempotent. Spec: BUNDLES_SYNC_SPEC.md §2.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF COL_LENGTH(N'dbo.ProductBundleComponentSwap', N'Qty') IS NULL
BEGIN
    ALTER TABLE dbo.ProductBundleComponentSwap ADD Qty DECIMAL(18, 4) NULL;
    PRINT 'ProductBundleComponentSwap.Qty added';
END
ELSE
    PRINT 'ProductBundleComponentSwap.Qty already exists';
GO
