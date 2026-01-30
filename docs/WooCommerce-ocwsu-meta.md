# WooCommerce "ocwsu" / Weighted Product Meta

## Where are the ocwsu properties documented?

**The `ocwsu_*` meta keys are not part of the official WooCommerce REST API.**  
They come from a **plugin on your WordPress/WooCommerce site** (e.g. a “Units of Sale” / “יחידות מכירה” type plugin). The official docs do not list them:

- [WooCommerce REST API – Product properties](https://woocommerce.github.io/woocommerce-rest-api-docs/#product-properties) – describes standard product fields and `meta_data` format, but not plugin-specific keys like `ocwsu_*`.

So you **cannot** see the “official” details of ocwsu in WooCommerce docs; you have to get them from your site.

---

## How to see the actual ocwsu meta on your site

### 1. From WooCommerce REST API (recommended)

1. In WooCommerce admin, **edit a product** that already has **“זה מוצר שקיל”** checked and save it.
2. Call the API to **get that product** (use the product’s WooCommerce ID):

   ```http
   GET https://YOUR-SITE/wp-json/wc/v3/products/{product_id}
   ```

   Use the same auth (e.g. consumer key/secret) as your integration.

3. In the JSON response, open the **`meta_data`** array.  
   Every entry has `key` and `value`.  
   There you will see the **exact** meta keys your plugin uses (e.g. `ocwsu_weighable_`, or a different name if the plugin uses another key for “זה מוצר שקיל”).

That way you see the **real** keys and values your site uses for weighted products.

### 2. From the WordPress database

1. Edit and save a product that has “זה מוצר שקיל” checked.
2. In the DB, open `wp_postmeta`.
3. Filter by `post_id` = that product’s post ID (same as product ID in WooCommerce).
4. Look at `meta_key` and `meta_value` for all rows.  
   The plugin will store its keys there (often prefixed; might be `ocwsu_*` or something else).

### 3. From the plugin’s code (if you have access)

If you have FTP/files or the plugin’s source:

- Search the plugin folder for strings like:
  - `ocwsu`
  - `weightable`
  - “זה מוצר שקיל” or the English label for that checkbox.
- The PHP that saves the product will use `update_post_meta` / `get_post_meta` with the **exact** `meta_key` that controls “זה מוצר שקיל”. That key is what the API and DB will show.

---

## What this backend uses today

The integration currently assumes the plugin uses these meta keys (until you confirm from your site):

| Meta key | Purpose |
|----------|--------|
| `ocwsu_weighable_` | “זה מוצר שקיל” – value `"yes"` or `"no"` |
| `ocwsu_sold_by_units_` | Sold by units |
| `ocwsu_sold_by_weight_` | Sold by weight |
| `ocwsu_product_weight_units_` | Weight units |
| `ocwsu_display_price_per_100g_` | Show price per 100g |
| `ocwsu_min_weight_`, `ocwsu_weight_step_`, etc. | Other weight/sale options |

If the plugin on your site uses **different keys** (or different values), update `WooCommerceService.cs` to use the keys you see in the API or DB.

---

## If “זה מוצר שקיל” still doesn’t update after sync

1. **Confirm the key** – Use “From WooCommerce REST API” above and check the exact `meta_data` for a product that is already marked as weighted. If the key is not `ocwsu_weighable_`, change the code to use the key you see.
2. **Confirm the value** – Some plugins use `"1"`/`"0"` instead of `"yes"`/`"no"`. If so, send the value your plugin expects.
3. **Confirm meta is writable** – Some plugins only read meta when it’s in a certain format or only allow updates from the admin UI. Check the plugin’s docs or code for “REST API” or “meta_data”.

Once you have the exact keys and values from your site (API or DB), you can align the backend with them and the checkbox should stay in sync.
