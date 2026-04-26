using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using George.DB;
using Microsoft.EntityFrameworkCore;

namespace George.Services;

/// <summary>WooCommerce → George import: images, tags, meta (weighable / weight config / SEO / brand / supplier), stock & visibility.</summary>
public partial class WooCommerceService
{
    private sealed class WooImportCatalogLookups
    {
        public List<StockStatus> StockStatuses { get; init; } = new();
        public List<StockManagementType> StockManagementTypes { get; init; } = new();
        public List<SetupType> SetupTypes { get; init; } = new();
        public List<Unit> Units { get; init; } = new();
        public List<UnitWeightMode> UnitWeightModes { get; init; } = new();
        public List<Visibility> Visibilities { get; init; } = new();
        public List<ProductStatus> ProductStatuses { get; init; } = new();
        public List<ShippingClass> ShippingClasses { get; init; } = new();
    }

    private static async Task<WooImportCatalogLookups> LoadWooImportCatalogLookupsAsync(GeorgeDBContext db, CancellationToken cancelToken)
    {
        return new WooImportCatalogLookups
        {
            StockStatuses = await db.StockStatus.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            StockManagementTypes = await db.StockManagementType.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            SetupTypes = await db.SetupType.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            Units = await db.Unit.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            UnitWeightModes = await db.UnitWeightMode.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            Visibilities = await db.Visibility.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            ProductStatuses = await db.ProductStatus.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
            ShippingClasses = await db.ShippingClass.Where(s => !s.IsDeleted).ToListAsync(cancelToken),
        };
    }

    private sealed class WooImportMetaEntry
    {
        public string? key { get; set; }
        public JsonElement value { get; set; }
    }

    private sealed class WooImportImageListItem
    {
        public int id { get; set; }
        public string? src { get; set; }
        public string? name { get; set; }
        public int position { get; set; }
    }

    private sealed class WooImportTagItem
    {
        public int id { get; set; }
        public string? name { get; set; }
    }

    private static string? MetaString(IReadOnlyList<WooImportMetaEntry>? meta, string key)
    {
        if (meta == null) return null;
        foreach (var m in meta)
        {
            if (string.IsNullOrWhiteSpace(m.key) || !string.Equals(m.key, key, StringComparison.OrdinalIgnoreCase))
                continue;
            if (m.value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                return null;
            var el = m.value;
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => el.GetRawText()
            };
        }
        return null;
    }

    private static string? MetaStringAny(IReadOnlyList<WooImportMetaEntry>? meta, params string[] keys)
    {
        foreach (var k in keys)
        {
            var v = MetaString(meta, k);
            if (!string.IsNullOrWhiteSpace(v))
                return v;
        }
        return null;
    }

    private static bool IsMetaYes(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return false;
        var s = v.Trim().ToLowerInvariant();
        return s is "yes" or "1" or "true";
    }

    private static int? MatchByNames(IEnumerable<(string Name, int Id)> rows, params string?[] candidates)
    {
        foreach (var cand in candidates)
        {
            if (string.IsNullOrWhiteSpace(cand)) continue;
            foreach (var (name, id) in rows)
            {
                if (string.Equals(name, cand.Trim(), StringComparison.OrdinalIgnoreCase))
                    return id;
            }
        }
        return null;
    }

    private static int? ResolveStockStatusId(WooImportCatalogLookups lk, string? wooStock)
    {
        if (string.IsNullOrWhiteSpace(wooStock)) return null;
        var w = wooStock.Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
        var rows = lk.StockStatuses.Select(s => (s.Name, s.Id));
        return w switch
        {
            "instock" => MatchByNames(rows, "instock", "in_stock", "inStock"),
            "outofstock" => MatchByNames(rows, "outofstock", "out_of_stock", "outOfStock"),
            "onbackorder" => MatchByNames(rows, "onbackorder", "on_backorder", "onBackorder"),
            _ => MatchByNames(rows, wooStock)
        };
    }

    private static int? ResolveVisibilityId(WooImportCatalogLookups lk, string? wooVis)
    {
        if (string.IsNullOrWhiteSpace(wooVis)) return null;
        var v = wooVis.Trim().ToLowerInvariant();
        var rows = lk.Visibilities.Select(s => (s.Name, s.Id));
        return v switch
        {
            "visible" => MatchByNames(rows, "visible"),
            "catalog" => MatchByNames(rows, "catalog"),
            "search" => MatchByNames(rows, "search"),
            "hidden" => MatchByNames(rows, "hidden", "outOfStock"),
            _ => MatchByNames(rows, wooVis)
        };
    }

    private static int? ResolveProductStatusId(WooImportCatalogLookups lk, string? wooStatus)
    {
        if (string.IsNullOrWhiteSpace(wooStatus)) return null;
        var s = wooStatus.Trim().ToLowerInvariant();
        var rows = lk.ProductStatuses.Select(x => (x.Name, x.Id));
        return s switch
        {
            "publish" => MatchByNames(rows, "active", "publish"),
            "draft" => MatchByNames(rows, "draft", "hidden"),
            "private" => MatchByNames(rows, "archived", "private"),
            "pending" => MatchByNames(rows, "draft", "pending"),
            _ => MatchByNames(rows, wooStatus)
        };
    }

    private static int? ResolveStockManagementTypeId(WooImportCatalogLookups lk, bool? manageStock, bool isVariable)
    {
        if (lk.StockManagementTypes.Count == 0)
            return null;
        var rows = lk.StockManagementTypes.Select(x => (x.Name, x.Id));
        if (manageStock == true)
            return MatchByNames(rows, "quantity");
        if (isVariable)
            return MatchByNames(rows, "variation", "status");
        return MatchByNames(rows, "status", "variation");
    }

    private static int? ResolveUnitIdFromOcwsu(List<Unit> units, string? ocwsuUnitsFromMeta)
    {
        if (string.IsNullOrWhiteSpace(ocwsuUnitsFromMeta)) return null;
        var target = ocwsuUnitsFromMeta.Trim().ToLowerInvariant();
        foreach (var u in units)
        {
            var mapped = MapOcwsuProductWeightUnits(u.Name);
            if (string.Equals(mapped, target, StringComparison.OrdinalIgnoreCase))
                return u.Id;
        }
        return null;
    }

    private static int? ResolveUnitWeightModeId(List<UnitWeightMode> modes, string? wooType, bool getWeightFromVariation)
    {
        if (string.IsNullOrWhiteSpace(wooType)) return null;
        var t = wooType.Trim().ToLowerInvariant();
        string? georgeName = t switch
        {
            "fixed" => "average",
            "variable" => getWeightFromVariation ? "by_variant" : "variable",
            _ => null
        };
        if (georgeName == null) return null;
        return modes.FirstOrDefault(m => string.Equals(m.Name, georgeName, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static int? ResolveSetupTypeId(List<SetupType> types, bool weighable, string? soldUnits, string? soldWeight)
    {
        if (!weighable) return null;
        var u = IsMetaYes(soldUnits);
        var w = IsMetaYes(soldWeight);
        string? name = (u, w) switch
        {
            (true, true) => "by_unit_and_weight",
            (true, false) => "by_unit",
            (false, true) => "by_weight",
            _ => "by_weight"
        };
        return types.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private static DateTime? TryParseWooDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;
        if (DateTime.TryParse(s, out dt))
            return dt;
        return null;
    }

    /// <summary>
    /// George treats variation salability as <c>StockQuantity &gt; 0</c> (binary or summed qty in UI).
    /// WooCommerce often leaves <c>manage_stock</c> false while still returning <c>stock_status</c> per variation.
    /// </summary>
    private static decimal? ResolveImportedVariantStockQuantity(WooImportVariationItem vv)
    {
        if (vv.manage_stock == true && vv.stock_quantity.HasValue)
            return vv.stock_quantity.Value;

        var st = (vv.stock_status ?? string.Empty).Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
        if (st is "instock" or "onbackorder")
            return 1m;
        if (st is "outofstock")
            return 0m;
        return null;
    }

    private async Task ApplyWooImportProductExtensionsAsync(
        GeorgeDBContext db,
        Product product,
        WooImportProductItem wp,
        int accountId,
        WooImportCatalogLookups lk,
        CancellationToken cancelToken)
    {
        var meta = wp.meta_data;
        var isVariable = string.Equals(wp.type, "variable", StringComparison.OrdinalIgnoreCase)
            || (wp.attributes?.Any(a => a.variation) ?? false);

        product.DisplayOrder = wp.menu_order;
        product.SalePriceStartDate = TryParseWooDate(wp.date_on_sale_from_gmt ?? wp.date_on_sale_from);
        product.SalePriceEndDate = TryParseWooDate(wp.date_on_sale_to_gmt ?? wp.date_on_sale_to);

        var stockId = ResolveStockStatusId(lk, wp.stock_status);
        if (stockId.HasValue) product.StockStatusId = stockId;
        var smtId = ResolveStockManagementTypeId(lk, wp.manage_stock, isVariable);
        if (smtId.HasValue) product.StockManagementTypeId = smtId;

        var visId = ResolveVisibilityId(lk, wp.catalog_visibility);
        if (visId.HasValue) product.VisibilityId = visId;
        var stId = ResolveProductStatusId(lk, wp.status);
        if (stId.HasValue) product.StatusId = stId;

        if (!string.IsNullOrWhiteSpace(wp.shipping_class))
        {
            var sc = lk.ShippingClasses.FirstOrDefault(x =>
                string.Equals(x.Name, wp.shipping_class.Trim(), StringComparison.OrdinalIgnoreCase));
            if (sc != null) product.ShippingClassId = sc.Id;
        }

        var cost = MetaStringAny(meta, "_cost_price");
        if (decimal.TryParse(cost?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var costDec))
            product.CostPrice = costDec;

        product.SeoTitle = MetaStringAny(meta, "_yoast_wpseo_title");
        product.SeoDescription = MetaStringAny(meta, "_yoast_wpseo_metadesc");

        var kosherMeta = MetaStringAny(meta, "_is_kosher");
        if (kosherMeta != null)
            product.IsKosher = IsMetaYes(kosherMeta);

        var showMl = MetaStringAny(meta, "show_as_ml", "_show_as_ml");
        if (!string.IsNullOrWhiteSpace(showMl) && !showMl.StartsWith("field_", StringComparison.OrdinalIgnoreCase))
        {
            product.ShowAsMl = IsMetaYes(showMl) || showMl.Trim() == "1";
            product.WeightUnit = product.ShowAsMl == true ? "ml" : product.WeightUnit;
        }

        var brandName = MetaStringAny(meta, "_brand");
        if (!string.IsNullOrWhiteSpace(brandName))
        {
            var bn = brandName.Trim();
            var b = await db.Brand.FirstOrDefaultAsync(
                x => !x.IsDeleted && x.AccountId == accountId && x.Name == bn, cancelToken);
            if (b == null)
            {
                b = new Brand
                {
                    Name = bn,
                    AccountId = accountId,
                    CreationTime = DateTime.UtcNow,
                    IsDeleted = false
                };
                db.Brand.Add(b);
                await db.SaveChangesAsync(cancelToken);
            }
            product.BrandId = b.Id;
        }

        var supplierName = MetaStringAny(meta, "_supplier");
        if (!string.IsNullOrWhiteSpace(supplierName))
        {
            var sn = supplierName.Trim();
            var s = await db.Supplier.FirstOrDefaultAsync(
                x => !x.IsDeleted && x.AccountId == accountId && x.Name == sn, cancelToken);
            if (s == null)
            {
                s = new Supplier
                {
                    Name = sn,
                    AccountId = accountId,
                    CreationTime = DateTime.UtcNow,
                    IsDeleted = false
                };
                db.Supplier.Add(s);
                await db.SaveChangesAsync(cancelToken);
            }
            product.SupplierId = s.Id;
        }

        var weighableMeta = MetaStringAny(meta, "_ocwsu_weighable", "ocwsu_weighable_", "ocwsu_weightable");
        if (weighableMeta != null)
            product.IsWeighted = IsMetaYes(weighableMeta);
        if (weighableMeta != null && !IsMetaYes(weighableMeta))
            product.SetupTypeId = null;

        var weighable = product.IsWeighted == true;
        var soldUnits = MetaStringAny(meta, "_ocwsu_sold_by_units", "ocwsu_sold_by_units_");
        var soldWeight = MetaStringAny(meta, "_ocwsu_sold_by_weight", "ocwsu_sold_by_weight_");
        var setupId = ResolveSetupTypeId(lk.SetupTypes, weighable, soldUnits, soldWeight);
        if (setupId.HasValue) product.SetupTypeId = setupId;

        if (weighable)
        {
            var ocwsuUnits = MetaStringAny(meta, "_ocwsu_product_weight_units", "ocwsu_product_weight_units_");
            var minW = MetaStringAny(meta, "_ocwsu_min_weight", "ocwsu_min_weight_");
            var step = MetaStringAny(meta, "_ocwsu_weight_step", "ocwsu_weight_step_");
            var unitW = MetaStringAny(meta, "_ocwsu_unit_weight", "ocwsu_unit_weight_");
            var unitType = MetaStringAny(meta, "_ocwsu_unit_weight_type", "ocwsu_unit_weight_type_");
            var wOpts = MetaStringAny(meta, "_ocwsu_unit_weight_options", "ocwsu_unit_weight_options_");
            var show100 = MetaStringAny(meta, "_ocwsu_display_price_per_100g", "ocwsu_display_price_per_100g_");
            var getFromVar = IsMetaYes(MetaStringAny(meta, "_ocwsu_get_weight_from_variation", "ocwsu_get_weight_from_variation_"));

            WeightConfig wc;
            if (product.WeightConfigId.HasValue)
            {
                wc = await db.WeightConfig.FirstAsync(w => w.Id == product.WeightConfigId.Value, cancelToken);
            }
            else
            {
                wc = new WeightConfig { IsDeleted = false };
                db.WeightConfig.Add(wc);
                await db.SaveChangesAsync(cancelToken);
                product.WeightConfigId = wc.Id;
            }

            wc.UnitId = ResolveUnitIdFromOcwsu(lk.Units, ocwsuUnits) ?? wc.UnitId;
            wc.StartWeight = string.IsNullOrWhiteSpace(minW) ? wc.StartWeight : minW.Trim();
            wc.Step = string.IsNullOrWhiteSpace(step) ? wc.Step : step.Trim();
            wc.UnitWeight = string.IsNullOrWhiteSpace(unitW) ? wc.UnitWeight : unitW.Trim();
            wc.WeightOptions = string.IsNullOrWhiteSpace(wOpts) ? wc.WeightOptions : wOpts.Replace("\n", ",").Trim();
            wc.ShowPricePer100g = IsMetaYes(show100) ? true : wc.ShowPricePer100g;
            wc.WeightByVariant = getFromVar ? true : wc.WeightByVariant;
            wc.UnitWeightModeId = ResolveUnitWeightModeId(lk.UnitWeightModes, unitType, getFromVar) ?? wc.UnitWeightModeId;
            if (string.Equals(unitType, "fixed", StringComparison.OrdinalIgnoreCase))
                wc.FixedWeightPerUnit = true;
            else if (string.Equals(unitType, "variable", StringComparison.OrdinalIgnoreCase))
                wc.FixedWeightPerUnit = false;
        }

        db.ProductImage.RemoveRange(db.ProductImage.Where(pi => pi.ProductId == product.Id));
        if (wp.images is { Count: > 0 })
        {
            var ordered = wp.images.OrderBy(i => i.position).ToList();
            var sort = 0;
            foreach (var img in ordered)
            {
                var url = img.src?.Trim();
                if (string.IsNullOrWhiteSpace(url)) continue;
                db.ProductImage.Add(new ProductImage
                {
                    ProductId = product.Id,
                    Url = url,
                    SortOrder = sort++,
                    MediaId = null
                });
            }
        }

        if (wp.tags != null)
        {
            product.Tag.Clear();
            foreach (var tg in wp.tags)
            {
                var name = tg.name?.Trim();
                if (string.IsNullOrWhiteSpace(name)) continue;
                var existing = await db.Tag.FirstOrDefaultAsync(
                    t => !t.IsDeleted && t.AccountId == accountId && t.Name == name, cancelToken);
                if (existing == null)
                {
                    existing = new Tag
                    {
                        Name = name,
                        AccountId = accountId,
                        CreationTime = DateTime.UtcNow,
                        IsDeleted = false
                    };
                    db.Tag.Add(existing);
                    await db.SaveChangesAsync(cancelToken);
                }
                product.Tag.Add(existing);
            }
        }

        await db.SaveChangesAsync(cancelToken);
    }

    private static bool WooImportProductLikelyHasVariations(WooImportProductItem wp) =>
        string.Equals(wp.type, "variable", StringComparison.OrdinalIgnoreCase)
        || (wp.attributes?.Any(a => a.variation) ?? false);

    /// <summary>Parallel GET of variation pages for one import batch (simple products skip the HTTP call).</summary>
    private async Task<Dictionary<int, List<WooImportVariationItem>>> PrefetchWooImportVariationsAsync(
        HttpClient httpClient,
        string baseUrl,
        IReadOnlyList<WooImportProductItem> batch,
        int maxParallel,
        CancellationToken cancelToken)
    {
        var map = new Dictionary<int, List<WooImportVariationItem>>();
        if (batch.Count == 0)
            return map;

        using var gate = new SemaphoreSlim(Math.Clamp(maxParallel, 1, 32));
        var tasks = batch.Select(async wp =>
        {
            if (!WooImportProductLikelyHasVariations(wp))
            {
                lock (map)
                {
                    map[wp.id] = new List<WooImportVariationItem>();
                }
                return;
            }

            await gate.WaitAsync(cancelToken).ConfigureAwait(false);
            try
            {
                var list = await FetchWooPagedAsync<WooImportVariationItem>(
                    httpClient,
                    $"{baseUrl}/products/{wp.id}/variations",
                    cancelToken).ConfigureAwait(false);
                lock (map)
                {
                    map[wp.id] = list;
                }
            }
            finally
            {
                gate.Release();
            }
        });
        await Task.WhenAll(tasks).ConfigureAwait(false);
        return map;
    }
}
