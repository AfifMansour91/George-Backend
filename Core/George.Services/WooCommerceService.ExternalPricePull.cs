using George.Common;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace George.Services
{
    /// <summary>
    /// External price pull (Woo → George) for sites with <see cref="Site.ExternalPriceManagement"/>: on those
    /// stores the POS writes prices directly to WooCommerce, George's outbound sync skips price fields, and this
    /// pull (daily job / manual endpoint) imports the store's current prices back into George so both stay equal.
    /// </summary>
    public partial class WooCommerceService
    {
        private const int ExternalPricePullPageSize = 100;

        /// <summary>Hard stop for the product list paging (100/page → 50k products).</summary>
        private const int ExternalPricePullMaxPages = 500;

        /// <summary>
        /// Pulls the store's current product/variation prices and applies them to George for this site.
        /// Network-managed sites write per-site overrides (so other branches' prices are untouched);
        /// single-store sites write the canonical product/variant prices. Only linked products (a Woo id
        /// known to George) are touched - nothing is created or deleted.
        /// </summary>
        public async Task<IApiResponse<WooPricePullRes>> PullPricesFromWooCommerceAsync(int siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooPricePullRes> { Data = new WooPricePullRes { SiteId = siteId } };
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
                if (site == null)
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                if (site.ExternalPriceManagement != true)
                    return CreateResponse(response, StatusCode.InvalidRequest, "External price management is not enabled for this site.");
                if (string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce integration not configured. Please set up your credentials in Store Settings.");
                }

                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.Timeout = WooCommerceHttpTimeout;
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                var networkManaged = await IsSiteNetworkManagedCachedAsync(siteId, cancelToken).ConfigureAwait(false);

                // wooProductId → George productId: the per-site map first (multi-store accounts), then the
                // legacy single Product.WooCommerceId column for products assigned to this site.
                var wooToProduct = await _overrideStorage.GetSiteWooProductIdMapForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
                var legacyMap = await _productStorage.GetWooProductIdMapForSiteAsync(siteId, cancelToken).ConfigureAwait(false);
                foreach (var kv in legacyMap)
                    if (!wooToProduct.ContainsKey(kv.Key))
                        wooToProduct[kv.Key] = kv.Value;

                var res = response.Data;
                _logger.LogInformation("External price pull started for site {SiteId} ({LinkedCount} linked products)", siteId, wooToProduct.Count);

                for (var page = 1; page <= ExternalPricePullMaxPages; page++)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    var url = $"{baseUrl}/products?per_page={ExternalPricePullPageSize}&page={page}&status=any&orderby=id&order=asc";
                    var listResponse = await httpClient.GetAsync(url, cancelToken);
                    if (!listResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await listResponse.Content.ReadAsStringAsync(cancelToken);
                        // Some stores answer a page past the end with 400 rest_invalid_param - that's the end, not a failure.
                        if (page > 1 && (int)listResponse.StatusCode == 400)
                            break;
                        throw new Exception(GetUserFriendlyWooCommerceError((int)listResponse.StatusCode, errorContent));
                    }

                    var items = await JsonSerializer.DeserializeAsync<List<WooImportProductItem>>(
                        await listResponse.Content.ReadAsStreamAsync(cancelToken),
                        cancellationToken: cancelToken) ?? new List<WooImportProductItem>();
                    if (items.Count == 0)
                        break;
                    res.WooProductsScanned += items.Count;

                    foreach (var wp in items)
                    {
                        cancelToken.ThrowIfCancellationRequested();
                        if (!wooToProduct.TryGetValue(wp.id, out var productId))
                        {
                            res.UnmatchedWooProducts++;
                            continue;
                        }
                        res.ProductsMatched++;
                        try
                        {
                            await ApplyExternalPricesForProductAsync(
                                baseUrl, siteId, site.AccountId, networkManaged, productId, wp, httpClient, res, cancelToken).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            res.Errors++;
                            _logger.LogWarning(ex, "External price pull: failed applying prices for Woo product {WooId} → product {ProductId} (site {SiteId})", wp.id, productId, siteId);
                        }
                    }

                    if (items.Count < ExternalPricePullPageSize)
                        break;
                }

                stopwatch.Stop();
                res.DurationMs = stopwatch.ElapsedMilliseconds;
                res.Message = $"Price pull done: {res.ProductsUpdated} products and {res.VariantsUpdated} variations updated " +
                              $"({res.ProductsMatched} matched of {res.WooProductsScanned} scanned, {res.Errors} errors).";
                _logger.LogInformation("External price pull for site {SiteId}: {Message}", siteId, res.Message);
                EnqueuePricePullLog(siteId, res, success: true, error: null);
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                response.Data!.DurationMs = stopwatch.ElapsedMilliseconds;
                _logger.LogError(ex, "External price pull failed for site {SiteId}", siteId);
                EnqueuePricePullLog(siteId, response.Data, success: false, error: ex.Message);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        /// <summary>Applies one Woo product's prices (and its variations') to George for this site.</summary>
        private async Task ApplyExternalPricesForProductAsync(
            string baseUrl,
            int siteId,
            int accountId,
            bool networkManaged,
            int productId,
            WooImportProductItem wp,
            HttpClient httpClient,
            WooPricePullRes res,
            CancellationToken cancelToken)
        {
            var price = ParseNullableDecimal(wp.regular_price);
            var salePrice = ParseNullableDecimal(wp.sale_price);
            var saleFrom = TryParseWooDate(wp.date_on_sale_from_gmt ?? wp.date_on_sale_from);
            var saleTo = TryParseWooDate(wp.date_on_sale_to_gmt ?? wp.date_on_sale_to);
            var isVariable = string.Equals(wp.type, "variable", StringComparison.OrdinalIgnoreCase);

            List<(int VariantId, decimal? Price, decimal? SalePrice)>? variantPrices = null;
            if (isVariable)
            {
                // wooVariationId → George variantId: per-site variation map (multi-store) or the legacy column.
                Dictionary<int, int> wooVarToVariant;
                if (networkManaged)
                {
                    var variantToWoo = await _overrideStorage.GetSiteVariantWooIdMapAsync(productId, siteId, cancelToken).ConfigureAwait(false);
                    wooVarToVariant = new Dictionary<int, int>();
                    foreach (var kv in variantToWoo)
                        wooVarToVariant[kv.Value] = kv.Key;
                    if (wooVarToVariant.Count == 0)
                        wooVarToVariant = await _productStorage.GetVariantWooIdMapForProductAsync(productId, cancelToken).ConfigureAwait(false);
                }
                else
                {
                    wooVarToVariant = await _productStorage.GetVariantWooIdMapForProductAsync(productId, cancelToken).ConfigureAwait(false);
                }

                variantPrices = new List<(int, decimal?, decimal?)>();
                for (var page = 1; page <= MaxVariationFetchPages; page++)
                {
                    var vUrl = $"{baseUrl}/products/{wp.id}/variations?per_page={VariationsPerPage}&page={page}";
                    var vResponse = await httpClient.GetAsync(vUrl, cancelToken);
                    if (!vResponse.IsSuccessStatusCode)
                        break;
                    var variations = await JsonSerializer.DeserializeAsync<List<WooImportVariationItem>>(
                        await vResponse.Content.ReadAsStreamAsync(cancelToken),
                        cancellationToken: cancelToken) ?? new List<WooImportVariationItem>();
                    if (variations.Count == 0)
                        break;
                    foreach (var vv in variations)
                    {
                        if (wooVarToVariant.TryGetValue(vv.id, out var variantId))
                            variantPrices.Add((variantId, ParseNullableDecimal(vv.regular_price), ParseNullableDecimal(vv.sale_price)));
                    }
                    if (variations.Count < VariationsPerPage)
                        break;
                }

                // A Woo variable parent has no own regular_price; mirror the importer and derive the parent
                // display price from the cheapest variation so product lists stay sensible.
                if (!price.HasValue && variantPrices.Count > 0)
                {
                    var regulars = variantPrices.Where(v => v.Price.HasValue).Select(v => v.Price!.Value).ToList();
                    if (regulars.Count > 0)
                        price = regulars.Min();
                }
            }

            if (networkManaged)
            {
                if (await _overrideStorage.UpsertExternalPricesAsync(productId, siteId, accountId, price, salePrice, saleFrom, saleTo, cancelToken).ConfigureAwait(false))
                    res.ProductsUpdated++;
                if (variantPrices != null && variantPrices.Count > 0)
                    res.VariantsUpdated += await _overrideStorage.UpsertExternalVariantPricesAsync(productId, siteId, variantPrices, cancelToken).ConfigureAwait(false);
            }
            else
            {
                var (productChanged, variantsChanged) = await _productStorage.ApplyExternalPricesAsync(
                    productId, price, salePrice, saleFrom, saleTo, variantPrices, cancelToken).ConfigureAwait(false);
                if (productChanged)
                    res.ProductsUpdated++;
                res.VariantsUpdated += variantsChanged;
            }
        }

        /// <summary>One IntegrationLog summary row per pull run (inbound, EntityType product).</summary>
        private void EnqueuePricePullLog(int siteId, WooPricePullRes res, bool success, string? error)
        {
            try
            {
                _integrationLogQueue.TryEnqueue(new IntegrationLog
                {
                    SiteId = siteId,
                    EntityType = "product",
                    Direction = "inbound",
                    Operation = "wc/v3 price pull",
                    Level = !success ? "error" : (res.Errors > 0 ? "warning" : "info"),
                    Success = success,
                    ResponseBody = JsonSerializer.Serialize(res),
                    Error = error?.Length > 1000 ? error[..1000] : error,
                    DurationMs = (int)Math.Min(res.DurationMs, int.MaxValue),
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
            catch
            {
                // best-effort logging only
            }
        }
    }
}
