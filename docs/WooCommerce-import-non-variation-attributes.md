# WooCommerce import: non-variation product attributes (TEMPORARY skip)

## Context

WooCommerce products can have two kinds of entries in the `attributes[]` array on `GET /wc/v3/products/{id}`:

| `variation` | WooCommerce meaning | Example from product 3611 |
|-------------|---------------------|---------------------------|
| `true` | Used to build variations (selectable options) | `צורת חיתוך`, `גודל` |
| `false` | Product specs / extra info shown on the product page, **not** variation axes | `קלוריות`, `חלבון (גרם)`, `שומן (גרם)`, … |

Sample payload: `shop-manager/woo-product-3611.json` (and variations in `woo-variations-3611.json`).

## Original import behavior (before temporary change)

In `WooCommerceService.UpsertProductsFromWooAsync`, **every** attribute with a non-empty `name` was imported as a George `ProductOption` + `ProductOptionValue` rows:

```csharp
var optionNameToValues = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
if (wp.attributes != null)
{
    foreach (var attr in wp.attributes.Where(a => !string.IsNullOrWhiteSpace(a.name)))
    {
        if (!optionNameToValues.TryGetValue(attr.name!.Trim(), out var values))
        {
            values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            optionNameToValues[attr.name.Trim()] = values;
        }
        foreach (var value in attr.options ?? new List<string>())
        {
            if (!string.IsNullOrWhiteSpace(value))
                values.Add(value.Trim());
        }
    }
}

foreach (var kv in optionNameToValues)
{
    var po = new ProductOption { ProductId = product.Id, Name = kv.Key, IsDeleted = false };
    db.ProductOption.Add(po);
    await db.SaveChangesAsync(cancelToken);
    foreach (var value in kv.Value)
        db.ProductOptionValue.Add(new ProductOptionValue { ProductOptionId = po.Id, Value = value });
}
```

That mixed nutritional/spec fields with real variation dimensions in `ProductOption`, which polluted the catalog UI.

**Variation line items** are unaffected by this block: each variation’s `attributes[]` is still imported into `ProductVariantOptionValue` from `GET /products/{id}/variations` (see loop over `vv.attributes` in the same method).

## Temporary behavior (current)

Only attributes with **`variation == true`** are imported into `ProductOption`:

```csharp
foreach (var attr in wp.attributes.Where(a => !string.IsNullOrWhiteSpace(a.name) && a.variation))
```

Marked in code with `// TEMPORARY` near the loop in `WooCommerceService.cs` (~line 4280).

## Sync to WooCommerce (preserve on update)

George `ProductOption` only contains variation attributes, but WooCommerce **replaces** the whole `attributes[]` on `PUT /products/{id}`. Without merging, any edit + sync would delete Woo-side `variation=false` rows (nutrition, specs).

**Current fix (TEMPORARY, paired with import skip):**

1. Before updating an existing Woo product, `GET /products/{id}` (same request used for image dedup).
2. Collect attributes where `variation == false`.
3. Build the PUT payload as: **preserved non-variation attrs** + **George variation attrs** (`ProductOption`), with variation positions offset after preserved rows.

Helpers in `WooCommerceService.cs`:

- `GetWooCommerceExistingProductForSyncAsync`
- `BuildWooSyncPreservedNonVariationAttributes`

**Limits:** George cannot edit these fields until we model them locally; sync only **keeps** what Woo already had. New products created only in George (no prior Woo attrs) are unchanged. If the GET fails, sync falls back to George variation attrs only (same as before the preserve fix).

## How to restore full import later

1. Remove `&& a.variation` from the `Where` clause (restore the original loop above).
2. Or implement a dedicated store for `variation=false` data (e.g. product specs table, custom fields, or read-only display from Woo meta) and import into that instead of `ProductOption`.

## Related code

- Attribute model: `WooImportProductAttributeItem` (`name`, `variation`, `options`) in `WooCommerceService.cs`
- Variable product detection still uses `wp.attributes?.Any(a => a.variation)` in `ApplyWooImportProductExtensionsAsync` — unchanged
- Export/sync **to** WooCommerce: variation attrs from `ProductOption`; non-variation attrs preserved from existing Woo product on update (see above)
