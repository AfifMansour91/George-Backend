using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using George.Data;
using George.DB;
using George.Services.Bundles;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    /// <summary>
    /// Bundles (מארזים) half of the WooCommerce sync: after the regular product sync has created/updated the
    /// <c>oc_bundle</c> post, PUT the bundle definition to the OC Bundles plugin
    /// (<c>{WooCommerceUrl}/wp-json/oc-bundles/v1/bundles/{wooId}</c>, header <c>X-OC-Bundles-Key</c>),
    /// and proxy its availability endpoint. Spec: BUNDLES_SYNC_SPEC.md §7 step 2, §3.2.
    /// </summary>
    public partial class WooCommerceService
    {
        public const string OcBundlesApiKeyHeader = "X-OC-Bundles-Key";
        public const string OcBundlesMissingApiKeyError = "חסר מפתח API למארזים";
        private static readonly TimeSpan OcBundlesHttpTimeout = TimeSpan.FromSeconds(60);

        /// <summary>Outcome of the OC Bundles definition PUT.</summary>
        public sealed class BundleDefinitionSyncResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
        }

        /// <summary>Base of the OC Bundles REST namespace for a site (null when the site has no Woo URL).</summary>
        public static string? OcBundlesBaseUrl(Site site)
        {
            if (string.IsNullOrWhiteSpace(site.WooCommerceUrl)) return null;
            return $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/oc-bundles/v1";
        }

        /// <summary>Spec §7 step 2 failure text when a component/swap product has no Woo id on this site.</summary>
        public static string ComponentNotSyncedError(string componentName) => $"רכיב {componentName} לא מסונכרן לאתר";

        /// <summary>
        /// Builds the OC Bundles PUT body for a bundle definition with ids already resolved per site.
        /// Returns null and an error when a component/swap product is not synced to the site.
        /// </summary>
        public static Dictionary<string, object?>? BuildOcBundlesPutBody(
            int productId,
            BundleDefinition definition,
            IReadOnlyDictionary<int, int> wooProductIdByProductId,
            IReadOnlyDictionary<int, int> wooVariationIdByVariantId,
            out string? error)
        {
            error = null;
            // A slot/swap pointing at a deleted catalog product must never be pushed: the store would keep selling
            // a component George no longer has. Surface it on the sync status instead.
            var deletedName = BundleService.FirstDeletedProductName(definition);
            if (deletedName != null)
            {
                error = BundleService.DeletedComponentError(deletedName);
                return null;
            }
            var components = new List<object>();
            foreach (var c in definition.Components.OrderBy(c => c.SortOrder).ThenBy(c => c.Id))
            {
                if (!wooProductIdByProductId.TryGetValue(c.ComponentProductId, out var wooPid))
                {
                    error = ComponentNotSyncedError(c.ComponentProduct?.Name ?? $"#{c.ComponentProductId}");
                    return null;
                }
                var variationId = 0;
                if (c.ComponentVariantId.HasValue)
                {
                    if (!wooVariationIdByVariantId.TryGetValue(c.ComponentVariantId.Value, out variationId))
                    {
                        error = ComponentNotSyncedError(c.ComponentProduct?.Name ?? $"#{c.ComponentProductId}");
                        return null;
                    }
                }

                // Swaps are meaningful only on a swappable slot: a non-swappable slot always sends an empty list
                // (and its stale swap rows never block the sync on a missing Woo id).
                var swaps = new List<object>();
                foreach (var s in c.Swappable ? c.Swaps.OrderBy(s => s.SortOrder).ThenBy(s => s.Id) : Enumerable.Empty<ProductBundleComponentSwap>())
                {
                    if (!wooProductIdByProductId.TryGetValue(s.SwapProductId, out var swapWooPid))
                    {
                        error = ComponentNotSyncedError(s.SwapProduct?.Name ?? $"#{s.SwapProductId}");
                        return null;
                    }
                    var swapVariationId = 0;
                    if (s.SwapVariantId.HasValue && !wooVariationIdByVariantId.TryGetValue(s.SwapVariantId.Value, out swapVariationId))
                    {
                        error = ComponentNotSyncedError(s.SwapProduct?.Name ?? $"#{s.SwapProductId}");
                        return null;
                    }
                    swaps.Add(new Dictionary<string, object?>
                    {
                        ["product_id"] = swapWooPid,
                        ["variation_id"] = swapVariationId,
                        ["surcharge"] = s.Surcharge,
                    });
                }

                components.Add(new Dictionary<string, object?>
                {
                    ["key"] = string.IsNullOrEmpty(c.ComponentKey) ? "c" + c.Id : c.ComponentKey,
                    ["product_id"] = wooPid,
                    ["variation_id"] = variationId,
                    ["qty"] = c.Qty,
                    // George stores weight-slot qty in kg; tell the plugin so it never interprets it as grams.
                    ["unit"] = BundleOrderLineBuilder.IsWeightSlot(c, c.ComponentProduct) ? "kg" : "unit",
                    ["swappable"] = c.Swappable,
                    ["description"] = c.Description ?? string.Empty,
                    ["swaps"] = swaps,
                });
            }

            var cfg = definition.Config;
            return new Dictionary<string, object?>
            {
                ["external_id"] = $"george-{productId}",
                ["pricing"] = new Dictionary<string, object?>
                {
                    ["mode"] = cfg.PricingMode,
                    ["fixed_price"] = cfg.FixedPrice,
                    ["discount_type"] = cfg.DiscountType,
                    ["discount_value"] = cfg.DiscountValue,
                    ["oos_behavior"] = cfg.OosBehavior,
                },
                ["layout"] = cfg.Layout,
                ["cart_display"] = cfg.CartDisplay,
                ["invoice_display"] = cfg.InvoiceDisplay,
                ["hide_price_labels"] = cfg.HidePriceLabels,
                ["reweigh_price"] = cfg.ReweighPrice,
                ["show_components_in_desc"] = cfg.ShowComponentsInDesc,
                ["components"] = components,
            };
        }

        /// <summary>
        /// Maps an OC Bundles error response to the text stored in <c>ProductSiteWooSyncStatus.Error</c>
        /// (plugin <c>code</c> + <c>message</c> when the body is WP-style JSON, else the HTTP status).
        /// </summary>
        public static string MapOcBundlesError(int statusCode, string? body)
        {
            var text = (body ?? string.Empty).Trim();
            if (text.Length > 0)
            {
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var root = doc.RootElement;
                        var code = root.TryGetProperty("code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String ? codeEl.GetString() : null;
                        var message = root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String ? msgEl.GetString() : null;
                        if (!string.IsNullOrWhiteSpace(code) || !string.IsNullOrWhiteSpace(message))
                            return string.IsNullOrWhiteSpace(code) ? message! : (string.IsNullOrWhiteSpace(message) ? code! : $"{code}: {message}");
                    }
                }
                catch (JsonException)
                {
                    // Not JSON (HTML error page etc.) - fall through to the status-based text.
                }
            }
            return statusCode switch
            {
                401 or 403 => $"OC Bundles: מפתח API נדחה על ידי האתר ({statusCode})",
                404 => "OC Bundles: תוסף המארזים לא נמצא באתר (404)",
                _ => $"OC Bundles: שגיאת HTTP {statusCode}" + (text.Length > 0 ? " - " + (text.Length > 300 ? text[..300] : text) : string.Empty),
            };
        }

        /// <summary>
        /// Reads <c>components[].unit / mode / unit_weight</c> from an OC Bundles bundle response, keyed by
        /// component key ("c" + id) and by index as a fallback.
        /// </summary>
        public static Dictionary<int, BundleComponentWooMirror> ParseOcBundlesComponentMirror(string? body, IReadOnlyList<ProductBundleComponent> orderedComponents)
        {
            var result = new Dictionary<int, BundleComponentWooMirror>();
            if (string.IsNullOrWhiteSpace(body)) return result;
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return result;
                if (!doc.RootElement.TryGetProperty("components", out var compsEl) || compsEl.ValueKind != JsonValueKind.Array) return result;

                var byKey = orderedComponents.ToDictionary(
                    c => string.IsNullOrEmpty(c.ComponentKey) ? "c" + c.Id : c.ComponentKey,
                    c => c,
                    StringComparer.Ordinal);
                var index = 0;
                foreach (var el in compsEl.EnumerateArray())
                {
                    ProductBundleComponent? target = null;
                    if (el.ValueKind == JsonValueKind.Object)
                    {
                        var key = el.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == JsonValueKind.String ? keyEl.GetString() : null;
                        if (!string.IsNullOrEmpty(key) && byKey.TryGetValue(key, out var byKeyMatch))
                            target = byKeyMatch;
                        else if (index < orderedComponents.Count)
                            target = orderedComponents[index];

                        if (target != null)
                        {
                            result[target.Id] = new BundleComponentWooMirror
                            {
                                Unit = ReadString(el, "unit"),
                                Mode = ReadString(el, "mode"),
                                UnitWeightKg = ReadDecimal(el, "unit_weight"),
                            };
                        }
                    }
                    index++;
                }
            }
            catch (JsonException)
            {
                // Ignore: the mirror is informational.
            }
            return result;

            static string? ReadString(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var v)) return null;
                return v.ValueKind switch
                {
                    JsonValueKind.String => v.GetString(),
                    JsonValueKind.Number => v.GetRawText(),
                    _ => null,
                };
            }

            static decimal? ReadDecimal(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var v)) return null;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) return p;
                return null;
            }
        }

        /// <summary>
        /// §7 step 2: PUT the bundle definition of <paramref name="productId"/> to the OC Bundles plugin of
        /// <paramref name="siteId"/> for the (already created/updated) Woo product <paramref name="wooProductId"/>,
        /// then store back the per-component unit/mode/unit_weight the plugin reports. Never throws.
        /// </summary>
        public async Task<BundleDefinitionSyncResult> SyncBundleDefinitionToWooAsync(int siteId, int productId, int wooProductId, CancellationToken cancelToken)
        {
            try
            {
                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
                if (site == null)
                    return new BundleDefinitionSyncResult { Success = false, Error = "Site not found" };

                var apiKey = site.BundlesApiKey?.Trim();
                if (string.IsNullOrEmpty(apiKey))
                    return new BundleDefinitionSyncResult { Success = false, Error = OcBundlesMissingApiKeyError };

                var baseUrl = OcBundlesBaseUrl(site);
                if (baseUrl == null)
                    return new BundleDefinitionSyncResult { Success = false, Error = "WooCommerce URL is not configured for this site" };

                var definition = await _bundleStorage.GetDefinitionAsync(productId, cancelToken).ConfigureAwait(false);
                if (definition == null)
                    return new BundleDefinitionSyncResult { Success = false, Error = "למארז אין הגדרת רכיבים" };
                if (definition.Components.Count == 0)
                    return new BundleDefinitionSyncResult { Success = false, Error = "למארז אין רכיבים" };

                // Component/swap ids resolved PER SITE.
                var productIds = definition.Components.Select(c => c.ComponentProductId)
                    .Concat(definition.Components.SelectMany(c => c.Swaps).Select(s => s.SwapProductId))
                    .Distinct().ToList();
                var variantIds = definition.Components.Where(c => c.ComponentVariantId.HasValue).Select(c => c.ComponentVariantId!.Value)
                    .Concat(definition.Components.SelectMany(c => c.Swaps).Where(s => s.SwapVariantId.HasValue).Select(s => s.SwapVariantId!.Value))
                    .Distinct().ToList();
                var wooProductMap = await _bundleStorage.GetSiteWooProductIdMapAsync(productIds, siteId, cancelToken).ConfigureAwait(false);
                var wooVariantMap = await _bundleStorage.GetSiteVariantWooIdMapAsync(variantIds, siteId, cancelToken).ConfigureAwait(false);

                var body = BuildOcBundlesPutBody(productId, definition, wooProductMap, wooVariantMap, out var buildError);
                if (body == null)
                    return new BundleDefinitionSyncResult { Success = false, Error = buildError };

                using var http = _httpClientFactory.CreateClient();
                http.Timeout = OcBundlesHttpTimeout;
                http.DefaultRequestHeaders.Clear();
                http.DefaultRequestHeaders.Add(OcBundlesApiKeyHeader, apiKey);

                var url = $"{baseUrl}/bundles/{wooProductId}";
                var json = JsonSerializer.Serialize(body);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await http.PutAsync(url, content, cancelToken).ConfigureAwait(false);
                var respBody = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var err = MapOcBundlesError((int)resp.StatusCode, respBody);
                    _logger.LogWarning("OC Bundles PUT failed for product {ProductId} (Woo {WooId}) on site {SiteId}: {Status} {Body}",
                        productId, wooProductId, siteId, (int)resp.StatusCode, respBody);
                    return new BundleDefinitionSyncResult { Success = false, Error = err };
                }

                // Store back what the plugin derived per component (unit / mode / unit weight).
                try
                {
                    var ordered = definition.Components.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToList();
                    var mirror = ParseOcBundlesComponentMirror(respBody, ordered);
                    if (mirror.Count > 0)
                        await _bundleStorage.UpdateComponentWooMirrorAsync(mirror, cancelToken).ConfigureAwait(false);
                }
                catch (Exception mirrorEx)
                {
                    _logger.LogWarning(mirrorEx, "OC Bundles: failed to store component unit/mode mirror for product {ProductId} site {SiteId}", productId, siteId);
                }

                _logger.LogInformation("OC Bundles PUT ok for product {ProductId} (Woo {WooId}) on site {SiteId}", productId, wooProductId, siteId);
                return new BundleDefinitionSyncResult { Success = true };
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OC Bundles PUT crashed for product {ProductId} (Woo {WooId}) on site {SiteId}", productId, wooProductId, siteId);
                return new BundleDefinitionSyncResult { Success = false, Error = "OC Bundles: " + ex.Message };
            }
        }

        /// <summary>
        /// Proxies <c>GET /bundles/{wooId}/availability</c> of the OC Bundles plugin. Returns null when the
        /// site has no API key / Woo URL, or the plugin answered with an error (the error text is returned).
        /// </summary>
        public async Task<(BundleAvailabilityRes? Availability, string? Error)> GetBundleAvailabilityFromWooAsync(Site site, int wooProductId, CancellationToken cancelToken)
        {
            var apiKey = site.BundlesApiKey?.Trim();
            if (string.IsNullOrEmpty(apiKey))
                return (null, OcBundlesMissingApiKeyError);
            var baseUrl = OcBundlesBaseUrl(site);
            if (baseUrl == null)
                return (null, "WooCommerce URL is not configured for this site");

            using var http = _httpClientFactory.CreateClient();
            http.Timeout = OcBundlesHttpTimeout;
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.Add(OcBundlesApiKeyHeader, apiKey);

            var url = $"{baseUrl}/bundles/{wooProductId}/availability";
            HttpResponseMessage resp;
            try
            {
                resp = await http.GetAsync(url, cancelToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                return (null, "OC Bundles: " + ex.Message);
            }
            var body = await resp.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (null, MapOcBundlesError((int)resp.StatusCode, body));

            return (ParseBundleAvailability(wooProductId, body), null);
        }

        /// <summary>
        /// Lenient parse of the availability payload (<c>{ price, available_quantity|availableQuantity, in_stock|inStock, components[] }</c>).
        /// Plugin components carry <c>{ key, product_id, variation_id, qty, unit, in_stock, stock }</c> - <c>stock</c> is the
        /// available quantity (older shapes: <c>available_quantity</c> / <c>availableQuantity</c>); <c>name</c> / <c>mode</c> may be absent.
        /// </summary>
        public static BundleAvailabilityRes ParseBundleAvailability(int wooProductId, string? body)
        {
            var res = new BundleAvailabilityRes { WooProductId = wooProductId, Raw = body };
            if (string.IsNullOrWhiteSpace(body)) return res;
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) return res;
                res.Price = GetDecimal(root, "price");
                res.AvailableQuantity = GetDecimal(root, "available_quantity") ?? GetDecimal(root, "availableQuantity");
                res.InStock = GetBool(root, "in_stock") ?? GetBool(root, "inStock");
                if (root.TryGetProperty("components", out var comps) && comps.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var c in comps.EnumerateArray())
                    {
                        if (c.ValueKind != JsonValueKind.Object) { index++; continue; }
                        res.Components.Add(new BundleAvailabilityComponentRes
                        {
                            Index = (int?)GetDecimal(c, "index") ?? index,
                            Key = GetString(c, "key"),
                            ProductId = (int?)GetDecimal(c, "product_id") ?? (int?)GetDecimal(c, "productId"),
                            VariationId = (int?)GetDecimal(c, "variation_id") ?? (int?)GetDecimal(c, "variationId"),
                            Name = GetString(c, "name"),
                            Qty = GetDecimal(c, "qty") ?? GetDecimal(c, "quantity"),
                            InStock = GetBool(c, "in_stock") ?? GetBool(c, "inStock"),
                            AvailableQuantity = GetDecimal(c, "stock") ?? GetDecimal(c, "available_quantity") ?? GetDecimal(c, "availableQuantity"),
                            Unit = GetString(c, "unit"),
                            Mode = GetString(c, "mode"),
                        });
                        index++;
                    }
                }
            }
            catch (JsonException)
            {
                // Raw is kept for the caller.
            }
            return res;

            static string? GetString(JsonElement el, string name) =>
                el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            static decimal? GetDecimal(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var v)) return null;
                if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
                if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p)) return p;
                return null;
            }

            static bool? GetBool(JsonElement el, string name)
            {
                if (!el.TryGetProperty(name, out var v)) return null;
                return v.ValueKind switch
                {
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Number => v.TryGetInt32(out var n) ? n != 0 : null,
                    JsonValueKind.String => bool.TryParse(v.GetString(), out var b) ? b : null,
                    _ => null,
                };
            }
        }
    }
}
