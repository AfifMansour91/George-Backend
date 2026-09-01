using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using George.Data;
using George.DB;
using George.Services.Request;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>
/// Lean order-driven stock push: sends ONLY stock fields via WooCommerce batch endpoints, and only for
/// products/variations whose Woo-side values actually differ. Replaces the full per-product PUT that the
/// order flows (picking / completion / line removal) used to trigger — a full product save on every picked
/// line purged the store's page cache all day and multiplied load at purchase peaks (Zano 502 incident,
/// 2026-08-31: ~2,900 full syncs/day collapsed to ~a few dozen batch calls).
///
/// The stock values are computed EXACTLY as the full sync computes them — the parent-product fields mirror
/// <see cref="SyncProductAsync"/> and the per-variation fields mirror <see cref="SyncProductVariantsAsync"/>.
/// If either of those changes its stock logic, this file must change with it (both sides carry this note).
/// Products this push cannot address safely (no Woo id yet, or a variation without a mapped Woo id) fall
/// back to the full <see cref="SyncToWooCommerceAsync"/> so create/match behavior is unchanged.
/// </summary>
public partial class WooCommerceService
{
    private const int WooStockPushBatchSize = 100;

    /// <summary>Variation list paging guard (100/page; catalog variants per product are far fewer in practice).</summary>
    private const int WooStockPushVariationFetchMaxPages = 5;

    private static readonly TimeSpan WooStockPushHttpTimeout = TimeSpan.FromMinutes(5);

    public sealed class WooCatalogStockPushSummary
    {
        public int ProductsRequested { get; set; }
        public int ProductsPushed { get; set; }
        public int ProductsSkippedUnchanged { get; set; }
        public int ProductsSkippedNotInSite { get; set; }
        public int ProductsFellBackToFullSync { get; set; }
        public int ProductsFailed { get; set; }
        public int VariationsPushed { get; set; }
        public int VariationsSkippedUnchanged { get; set; }
    }

    /// <summary>One product's desired Woo stock state (parent fields + optional per-variation fields).</summary>
    private sealed class WooStockPushPlan
    {
        public int ProductId { get; init; }
        public int WooProductId { get; init; }
        public Dictionary<string, object> ParentFields { get; } = new();
        /// <summary>Quantity participates in the unchanged-check only when Woo actually manages it (manage_stock on) — Woo ignores the field otherwise, so comparing it would defeat every skip.</summary>
        public bool CompareParentQuantity { get; set; }
        public List<Dictionary<string, object>>? VariationFields { get; set; }
        public bool CompareVariationQuantity { get; set; }
    }

    private sealed class WooCurrentStock
    {
        public bool? ManageStock { get; init; }
        public decimal? StockQuantity { get; init; }
        public string? StockStatus { get; init; }
        public string? Backorders { get; init; }
    }

    /// <summary>
    /// Pushes current George stock for the given catalog products to one WooCommerce store using batch
    /// endpoints, skipping values the store already has. Values match the full product sync exactly.
    /// </summary>
    public async Task<WooCatalogStockPushSummary> PushCatalogStockToWooCommerceAsync(
        int siteId,
        IReadOnlyList<int> productIds,
        CancellationToken cancelToken)
    {
        var summary = new WooCatalogStockPushSummary { ProductsRequested = productIds.Count };

        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
        if (site == null ||
            string.IsNullOrEmpty(site.WooCommerceUrl) ||
            string.IsNullOrEmpty(site.WooCommerceKey) ||
            string.IsNullOrEmpty(site.WooCommerceSecret))
        {
            _logger.LogWarning("Catalog stock push skipped for site {SiteId}: WooCommerce credentials not configured.", siteId);
            return summary;
        }

        var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
        using var httpClient = _httpClientFactory.CreateClient();
        httpClient.Timeout = WooStockPushHttpTimeout;
        httpClient.DefaultRequestHeaders.Clear();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

        var plans = new List<WooStockPushPlan>();
        var fallbackProductIds = new List<int>();

        foreach (var productId in productIds.Where(id => id > 0).Distinct())
        {
            cancelToken.ThrowIfCancellationRequested();
            try
            {
                var plan = await BuildStockPushPlanAsync(siteId, productId, summary, fallbackProductIds, cancelToken).ConfigureAwait(false);
                if (plan != null)
                    plans.Add(plan);
            }
            catch (Exception ex)
            {
                summary.ProductsFailed++;
                _logger.LogWarning(ex, "Catalog stock push: failed to build plan for product {ProductId} site {SiteId}", productId, siteId);
            }
        }

        // Delta check: read the store's current stock fields once, then push only real differences.
        // A failed/partial read is treated as "different" — when in doubt, push (never silently skip).
        var currentByWooId = await FetchCurrentWooStockAsync(baseUrl, plans.Select(p => p.WooProductId).ToList(), httpClient, cancelToken).ConfigureAwait(false);

        var parentsToUpdate = new List<WooStockPushPlan>();
        foreach (var plan in plans)
        {
            currentByWooId.TryGetValue(plan.WooProductId, out var current);
            if (IsParentStockUnchanged(plan, current))
                summary.ProductsSkippedUnchanged++;
            else
                parentsToUpdate.Add(plan);
        }

        await PushParentStockBatchesAsync(baseUrl, parentsToUpdate, summary, httpClient, cancelToken).ConfigureAwait(false);

        foreach (var plan in plans.Where(p => p.VariationFields is { Count: > 0 }))
        {
            cancelToken.ThrowIfCancellationRequested();
            try
            {
                await PushVariationStockAsync(baseUrl, plan, summary, httpClient, cancelToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Catalog stock push: variation push failed for product {ProductId} (Woo id {WooId})", plan.ProductId, plan.WooProductId);
            }
        }

        if (fallbackProductIds.Count > 0)
        {
            summary.ProductsFellBackToFullSync = fallbackProductIds.Count;
            _logger.LogInformation(
                "Catalog stock push: {Count} product(s) not fully linked to Woo ids; falling back to full sync: {Ids}",
                fallbackProductIds.Count, string.Join(",", fallbackProductIds));
            await SyncToWooCommerceAsync(new WooCommerceSyncReq { SiteId = siteId, ProductIds = fallbackProductIds }, cancelToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Catalog stock push completed for site {SiteId}: requested={Requested}, pushed={Pushed}, unchanged={Unchanged}, variationsPushed={VarPushed}, variationsUnchanged={VarUnchanged}, fullSyncFallback={Fallback}, failed={Failed}",
            siteId, summary.ProductsRequested, summary.ProductsPushed, summary.ProductsSkippedUnchanged,
            summary.VariationsPushed, summary.VariationsSkippedUnchanged, summary.ProductsFellBackToFullSync, summary.ProductsFailed);
        return summary;
    }

    /// <summary>Computes one product's desired stock fields. Returns null when the product is skipped or routed to the full-sync fallback.</summary>
    private async Task<WooStockPushPlan?> BuildStockPushPlanAsync(
        int siteId,
        int productId,
        WooCatalogStockPushSummary summary,
        List<int> fallbackProductIds,
        CancellationToken cancelToken)
    {
        var product = await _productStorage.GetProductAsync(productId, cancelToken).ConfigureAwait(false);
        if (product == null || product.Site?.Any(s => s.Id == siteId) != true)
        {
            summary.ProductsSkippedNotInSite++;
            return null;
        }

        var wooId = await _overrideStorage.GetSiteWooProductIdAsync(product.Id, siteId, cancelToken).ConfigureAwait(false)
            ?? product.WooCommerceId;
        if (wooId is not > 0)
        {
            // Not linked to this store yet — the full sync owns SKU matching and product creation.
            fallbackProductIds.Add(productId);
            return null;
        }

        SiteOverrideValues? siteOverride = null;
        try
        {
            var ovList = await _overrideStorage.GetOverridesForSiteAsync(new[] { product.Id }, siteId, cancelToken).ConfigureAwait(false);
            siteOverride = ovList.Count > 0 ? ovList[0] : null;
        }
        catch (Exception ovEx)
        {
            _logger.LogWarning(ovEx, "Catalog stock push: failed to load per-site override for product {ProductId} site {SiteId}; using canonical values", product.Id, siteId);
        }

        // Effective stock status — mirrors SyncProductAsync ("Map stock status" + per-site override).
        var stockStatus = "instock";
        if (product.StockStatus?.Name == "out_of_stock" || product.Status?.Name == "outOfStock")
            stockStatus = "outofstock";
        else if (product.StockStatus?.Name == "on_backorder")
            stockStatus = "onbackorder";
        if (!string.IsNullOrEmpty(siteOverride?.StockStatus))
        {
            stockStatus = siteOverride!.StockStatus == "out_of_stock" ? "outofstock"
                : siteOverride.StockStatus == "on_backorder" ? "onbackorder"
                : "instock";
        }
        var backorders = product.StockStatus?.Name == "on_backorder" ? "yes" : "no";

        var plan = new WooStockPushPlan { ProductId = product.Id, WooProductId = wooId.Value };
        plan.ParentFields["id"] = wooId.Value;

        var activeVariants = product.ProductVariant?.Where(v => !v.IsDeleted).ToList() ?? new List<ProductVariant>();
        if (activeVariants.Count == 0)
        {
            // Simple product — mirrors SyncProductAsync's simple-product stock block, including the
            // forced quantity 0 on an explicit out-of-stock (Woo derives status from quantity, MultiSite #14).
            var manageStock = !string.IsNullOrEmpty(siteOverride?.StockManagementType)
                ? IsStockQuantityManagementName(siteOverride!.StockManagementType)
                : IsStockQuantityManagementName(product.StockManagementType?.Name);
            var stockQty = siteOverride?.StockQuantity ?? product.StockQuantity;

            plan.ParentFields["manage_stock"] = manageStock;
            plan.ParentFields["stock_quantity"] = (manageStock && stockStatus == "outofstock") ? 0m : (stockQty ?? 0m);
            plan.ParentFields["stock_status"] = stockStatus;
            plan.ParentFields["backorders"] = backorders;
            plan.CompareParentQuantity = manageStock;
            return plan;
        }

        // Variable product — parent block mirrors SyncProductAsync's variable-product stock branches.
        var smt = product.StockManagementType?.Name;
        if (IsStockQuantityManagementName(smt))
        {
            plan.ParentFields["manage_stock"] = true;
            plan.ParentFields["stock_quantity"] = (siteOverride?.StockQuantity ?? product.StockQuantity) ?? 0m;
            plan.ParentFields["stock_status"] = stockStatus;
            plan.ParentFields["backorders"] = backorders;
            plan.CompareParentQuantity = true;
        }
        else
        {
            // "status" and variation-level management both leave parent quantity untracked.
            plan.ParentFields["manage_stock"] = false;
            plan.ParentFields["stock_status"] = stockStatus;
            plan.ParentFields["backorders"] = backorders;
        }

        // Per-variation stock — mirrors SyncProductVariantsAsync (status derivation, forced out-of-stock,
        // per-site variant overrides, and integer quantity conversion).
        var stockManagedPerVariation = string.Equals(smt, "variation", StringComparison.OrdinalIgnoreCase);
        var variationTrackQuantity = stockManagedPerVariation && product.VariationStockByQuantity == true;
        var manageVariationStockInWoo = variationTrackQuantity;
        var productForcedOutOfStock = stockStatus == "outofstock";

        var isNetworkManaged = await IsSiteNetworkManagedCachedAsync(siteId, cancelToken).ConfigureAwait(false);
        Dictionary<int, int> siteVariantWooIds = new();
        if (isNetworkManaged)
        {
            try { siteVariantWooIds = await _overrideStorage.GetSiteVariantWooIdMapAsync(product.Id, siteId, cancelToken).ConfigureAwait(false); }
            catch (Exception vwEx) { _logger.LogWarning(vwEx, "Catalog stock push: failed to load per-site variation ids for product {ProductId} site {SiteId}", product.Id, siteId); }
        }
        Dictionary<int, ProductSiteOverrideStorage.VariantSiteOverride> perSiteVariantOverrides = new();
        try
        {
            perSiteVariantOverrides = await _overrideStorage.GetVariantOverridesForSiteAsync(product.Id, siteId, cancelToken).ConfigureAwait(false);
        }
        catch (Exception vsEx)
        {
            _logger.LogWarning(vsEx, "Catalog stock push: failed to load per-site variant overrides for product {ProductId} site {SiteId}; using canonical", product.Id, siteId);
        }

        var variationFields = new List<Dictionary<string, object>>();
        foreach (var variant in activeVariants)
        {
            if (variant.Id > 0 && perSiteVariantOverrides.TryGetValue(variant.Id, out var exclProbe) && exclProbe.IsExcluded)
                continue;

            var variantWooId = isNetworkManaged
                ? (siteVariantWooIds.TryGetValue(variant.Id, out var sid) ? sid : 0)
                : (variant.WooCommerceVariationId ?? 0);
            if (variantWooId <= 0)
            {
                // A variation without a mapped Woo id needs the full sync's signature matching / creation.
                fallbackProductIds.Add(productId);
                return null;
            }

            string variantStockStatus;
            if (variationTrackQuantity)
                variantStockStatus = (variant.StockQuantity ?? 0) > 0 ? "instock" : "outofstock";
            else if (stockManagedPerVariation && variant.StockQuantity.HasValue)
                variantStockStatus = variant.StockQuantity.Value > 0 ? "instock" : "outofstock";
            else
                variantStockStatus = stockStatus;
            if (productForcedOutOfStock)
                variantStockStatus = "outofstock";

            var fields = new Dictionary<string, object>
            {
                ["id"] = variantWooId,
                ["manage_stock"] = manageVariationStockInWoo,
                ["stock_status"] = variantStockStatus
            };
            if (manageVariationStockInWoo)
            {
                var siteVarOvr = (variant.Id > 0 && perSiteVariantOverrides.TryGetValue(variant.Id, out var ovrRow)) ? ovrRow : null;
                var effVariantStock = (siteVarOvr?.StockQuantity).HasValue ? siteVarOvr!.StockQuantity : variant.StockQuantity;
                fields["stock_quantity"] = productForcedOutOfStock
                    ? 0
                    : ToWooVariationStockQuantity(effVariantStock, variationTrackQuantity);
            }
            variationFields.Add(fields);
        }

        plan.VariationFields = variationFields;
        plan.CompareVariationQuantity = manageVariationStockInWoo;
        return plan;
    }

    /// <summary>Reads current stock fields for the given Woo product ids (chunked GET). Missing entries mean "unknown" → callers push.</summary>
    private async Task<Dictionary<int, WooCurrentStock>> FetchCurrentWooStockAsync(
        string baseUrl,
        IReadOnlyList<int> wooProductIds,
        HttpClient httpClient,
        CancellationToken cancelToken)
    {
        var result = new Dictionary<int, WooCurrentStock>();
        for (var i = 0; i < wooProductIds.Count; i += WooStockPushBatchSize)
        {
            var chunk = wooProductIds.Skip(i).Take(WooStockPushBatchSize).ToList();
            var url = $"{baseUrl}/products?include={string.Join(",", chunk)}&per_page={WooStockPushBatchSize}&_fields=id,manage_stock,stock_quantity,stock_status,backorders";
            try
            {
                var resp = await httpClient.GetAsync(url, cancelToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Catalog stock push: current-stock read returned {Status}; pushing chunk without delta check", (int)resp.StatusCode);
                    continue;
                }
                var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                foreach (var (id, current) in ParseWooStockArray(body))
                    result[id] = current;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Catalog stock push: current-stock read failed; pushing chunk without delta check");
            }
        }
        return result;
    }

    private static IEnumerable<(int id, WooCurrentStock current)> ParseWooStockArray(string json)
    {
        var items = new List<(int, WooCurrentStock)>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return items;
        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty("id", out var idEl) || !idEl.TryGetInt32(out var id))
                continue;
            items.Add((id, new WooCurrentStock
            {
                ManageStock = ReadFlexibleBool(el, "manage_stock"),
                StockQuantity = ReadFlexibleDecimal(el, "stock_quantity"),
                StockStatus = ReadString(el, "stock_status"),
                Backorders = ReadString(el, "backorders")
            }));
        }
        return items;
    }

    private static string? ReadString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Woo returns manage_stock as bool or the string "parent" (variations); "parent" reads as null → treated as a mismatch, so it is pushed like the full sync always did.</summary>
    private static bool? ReadFlexibleBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static decimal? ReadFlexibleDecimal(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var s)) return s;
        return null;
    }

    private static bool IsParentStockUnchanged(WooStockPushPlan plan, WooCurrentStock? current)
    {
        if (current == null) return false;
        if (current.ManageStock != (bool)plan.ParentFields["manage_stock"]) return false;
        if (!string.Equals(current.StockStatus, (string)plan.ParentFields["stock_status"], StringComparison.Ordinal)) return false;
        if (plan.ParentFields.TryGetValue("backorders", out var bo) &&
            !string.Equals(current.Backorders, (string)bo, StringComparison.Ordinal)) return false;
        if (plan.CompareParentQuantity)
        {
            var desired = Convert.ToDecimal(plan.ParentFields["stock_quantity"], CultureInfo.InvariantCulture);
            if (!current.StockQuantity.HasValue || current.StockQuantity.Value != desired) return false;
        }
        return true;
    }

    private static bool IsVariationStockUnchanged(Dictionary<string, object> desired, WooCurrentStock? current, bool compareQuantity)
    {
        if (current == null) return false;
        if (current.ManageStock != (bool)desired["manage_stock"]) return false;
        if (!string.Equals(current.StockStatus, (string)desired["stock_status"], StringComparison.Ordinal)) return false;
        if (compareQuantity)
        {
            var desiredQty = Convert.ToDecimal(desired["stock_quantity"], CultureInfo.InvariantCulture);
            if (!current.StockQuantity.HasValue || current.StockQuantity.Value != desiredQty) return false;
        }
        return true;
    }

    private async Task PushParentStockBatchesAsync(
        string baseUrl,
        IReadOnlyList<WooStockPushPlan> plansToUpdate,
        WooCatalogStockPushSummary summary,
        HttpClient httpClient,
        CancellationToken cancelToken)
    {
        for (var i = 0; i < plansToUpdate.Count; i += WooStockPushBatchSize)
        {
            cancelToken.ThrowIfCancellationRequested();
            var chunk = plansToUpdate.Skip(i).Take(WooStockPushBatchSize).ToList();
            var payload = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["update"] = chunk.Select(p => p.ParentFields).ToList()
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await httpClient.PostAsync($"{baseUrl}/products/batch", content, cancelToken).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                summary.ProductsPushed += chunk.Count;
                LogWooBatchItemErrors(await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false), "products/batch");
            }
            else
            {
                summary.ProductsFailed += chunk.Count;
                var err = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                _logger.LogWarning("Catalog stock push: products/batch failed ({Status}): {Body}", (int)resp.StatusCode, Truncate(err, 500));
            }
        }
    }

    private async Task PushVariationStockAsync(
        string baseUrl,
        WooStockPushPlan plan,
        WooCatalogStockPushSummary summary,
        HttpClient httpClient,
        CancellationToken cancelToken)
    {
        var currentById = new Dictionary<int, WooCurrentStock>();
        for (var page = 1; page <= WooStockPushVariationFetchMaxPages; page++)
        {
            var url = $"{baseUrl}/products/{plan.WooProductId}/variations?per_page={WooStockPushBatchSize}&page={page}&_fields=id,manage_stock,stock_quantity,stock_status";
            var resp = await httpClient.GetAsync(url, cancelToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                break; // unknown current state → push everything below
            var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
            var pageItems = ParseWooStockArray(body).ToList();
            foreach (var (id, current) in pageItems)
                currentById[id] = current;
            if (pageItems.Count < WooStockPushBatchSize)
                break;
        }

        var changed = new List<Dictionary<string, object>>();
        foreach (var fields in plan.VariationFields!)
        {
            currentById.TryGetValue((int)fields["id"], out var current);
            if (IsVariationStockUnchanged(fields, current, plan.CompareVariationQuantity))
                summary.VariationsSkippedUnchanged++;
            else
                changed.Add(fields);
        }
        if (changed.Count == 0)
            return;

        for (var i = 0; i < changed.Count; i += WooStockPushBatchSize)
        {
            var chunk = changed.Skip(i).Take(WooStockPushBatchSize).ToList();
            var payload = JsonSerializer.Serialize(new Dictionary<string, object> { ["update"] = chunk });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await httpClient.PostAsync($"{baseUrl}/products/{plan.WooProductId}/variations/batch", content, cancelToken).ConfigureAwait(false);
            if (resp.IsSuccessStatusCode)
            {
                summary.VariationsPushed += chunk.Count;
                LogWooBatchItemErrors(await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false), $"products/{plan.WooProductId}/variations/batch");
            }
            else
            {
                var err = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                _logger.LogWarning(
                    "Catalog stock push: variations/batch failed for Woo product {WooId} ({Status}): {Body}",
                    plan.WooProductId, (int)resp.StatusCode, Truncate(err, 500));
            }
        }
    }

    /// <summary>Woo batch endpoints return 200 with per-item "error" objects; surface those in the log.</summary>
    private void LogWooBatchItemErrors(string responseBody, string endpoint)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (!doc.RootElement.TryGetProperty("update", out var updateEl) || updateEl.ValueKind != JsonValueKind.Array)
                return;
            foreach (var item in updateEl.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object && item.TryGetProperty("error", out var errEl))
                {
                    var id = item.TryGetProperty("id", out var idEl) && idEl.TryGetInt32(out var i) ? i : 0;
                    _logger.LogWarning("Catalog stock push: {Endpoint} item error for Woo id {WooId}: {Error}", endpoint, id, Truncate(errEl.GetRawText(), 300));
                }
            }
        }
        catch (JsonException)
        {
            // Non-JSON body on a 200 is unexpected but not worth failing the push over.
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value.Substring(0, max);
}
