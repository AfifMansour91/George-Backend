-- One-time cleanup (MultiSite #5 / Sprint4 #16): WooCommerce REST echoes the PARENT product SKU on a
-- variation that has no own SKU (WC_Product_Variation::get_sku() falls back to the parent in 'view' context).
-- Earlier imports stored that echo on the variant, so children ended up holding the parent's SKU. That duplicate
-- SKU makes Woo reject variation updates (product_invalid_sku) and drives variations to pile up on re-sync.
-- This nulls every variant SKU that equals its parent product's SKU so children go back to "no own SKU".
-- Re-run safe (idempotent): after the first run there are no matches left.

UPDATE v
SET v.[Sku] = NULL
FROM [dbo].[ProductVariant] v
INNER JOIN [dbo].[Product] p ON p.[Id] = v.[ProductId]
WHERE v.[IsDeleted] = 0
  AND v.[Sku] IS NOT NULL
  AND p.[Sku] IS NOT NULL
  AND LOWER(LTRIM(RTRIM(v.[Sku]))) = LOWER(LTRIM(RTRIM(p.[Sku])));
GO
