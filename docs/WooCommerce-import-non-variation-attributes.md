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

## How to restore full import later

1. Remove `&& a.variation` from the `Where` clause (restore the original loop above).
2. Or implement a dedicated store for `variation=false` data (e.g. product specs table, custom fields, or read-only display from Woo meta) and import into that instead of `ProductOption`.

## Related code

- Attribute model: `WooImportProductAttributeItem` (`name`, `variation`, `options`) in `WooCommerceService.cs`
- Variable product detection still uses `wp.attributes?.Any(a => a.variation)` in `ApplyWooImportProductExtensionsAsync` — unchanged
- Export/sync **to** WooCommerce uses George `ProductOption` only; skipped Woo spec attributes will not be pushed back until we model them explicitly
