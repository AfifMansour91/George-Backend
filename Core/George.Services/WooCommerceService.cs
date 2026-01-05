using AutoMapper;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace George.Services
{
    public class WooCommerceService : ServiceBase
    {
        private readonly SiteStorage _siteStorage;
        private readonly CategoryStorage _categoryStorage;
        private readonly ProductStorage _productStorage;
        private readonly IHttpClientFactory _httpClientFactory;

        public WooCommerceService(
            ILogger<WooCommerceService> logger,
            IMapper mapper,
            CacheManager cache,
            SiteStorage siteStorage,
            CategoryStorage categoryStorage,
            ProductStorage productStorage,
            IHttpClientFactory httpClientFactory
        ) : base(logger, mapper, cache)
        {
            _siteStorage = siteStorage;
            _categoryStorage = categoryStorage;
            _productStorage = productStorage;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<IApiResponse<WooCommerceSyncRes>> SyncToWooCommerceAsync(
            WooCommerceSyncReq req,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceSyncRes>
            {
                Data = new WooCommerceSyncRes()
            };

            try
            {
                // Get site with WooCommerce credentials
                var site = await _siteStorage.GetSiteAsync(req.SiteId, cancelToken);
                if (site == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                }

                if (string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce integration not configured. Please set up your credentials in Store Settings.");
                }

                // Setup WooCommerce API client
                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));
                
                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");
                // Note: Content-Type is set on HttpContent objects (StringContent), not on DefaultRequestHeaders

                // Sync categories first
                var categoryMap = await SyncCategoriesAsync(baseUrl, req.SiteId, httpClient, cancelToken);

                // Sync products
                var syncResults = await SyncProductsAsync(
                    baseUrl,
                    req.SiteId,
                    req.ProductIds,
                    categoryMap,
                    httpClient,
                    cancelToken);

                response.Data.Success = syncResults.Where(r => r.Success).ToList();
                response.Data.Failed = syncResults.Where(r => !r.Success).ToList();
                response.Data.Message = $"Synced {response.Data.Success.Count} products successfully, {response.Data.Failed.Count} failed";

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing to WooCommerce");
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        private async Task<Dictionary<int, int>> SyncCategoriesAsync(
            string baseUrl,
            int siteId,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var categoryMap = new Dictionary<int, int>();

            // Get all categories linked to this site through the CategorySite junction table
            var categoriesResult = await _categoryStorage.GetCategoriesAsync(
                new CategoryFilter { SiteId = siteId },
                new PagingExDto { Skip = 0, Take = 10000, IncludeTotal = false },
                cancelToken);

            // Filter out deleted and inactive categories
            var categories = categoriesResult.Items
                .Where(c => !c.IsDeleted && c.IsActive)
                .ToList();

            _logger.LogInformation("Found {Count} categories to sync for site {SiteId} (from CategorySite table)", 
                categories.Count, siteId);

            // Sync main categories first
            var mainCategories = categories
                .Where(c => c.ParentCategoryId == null)
                .ToList();

            foreach (var category in mainCategories)
            {
                try
                {
                    var wooCatId = await SyncCategoryAsync(baseUrl, category, null, httpClient, cancelToken);
                    if (wooCatId.HasValue)
                    {
                        categoryMap[category.Id] = wooCatId.Value;
                        await _categoryStorage.UpdateCategoryWooCommerceIdAsync(category.Id, wooCatId.Value, cancelToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync category {CategoryName}", category.Name);
                }
            }

            // Sync subcategories
            var subCategories = categories
                .Where(c => c.ParentCategoryId != null && categoryMap.ContainsKey(c.ParentCategoryId.Value))
                .ToList();

            foreach (var category in subCategories)
            {
                try
                {
                    var parentWooId = categoryMap[category.ParentCategoryId!.Value];
                    var wooCatId = await SyncCategoryAsync(baseUrl, category, parentWooId, httpClient, cancelToken);
                    if (wooCatId.HasValue)
                    {
                        categoryMap[category.Id] = wooCatId.Value;
                        await _categoryStorage.UpdateCategoryWooCommerceIdAsync(category.Id, wooCatId.Value, cancelToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync subcategory {CategoryName}", category.Name);
                }
            }

            return categoryMap;
        }

        private async Task<int?> SyncCategoryAsync(
    string baseUrl,
    Category category,
    int? parentWooId,
    HttpClient httpClient,
    CancellationToken cancelToken)
        {
            var wooCatData = new
            {
                name = category.Name,
                description = category.Description ?? "",
                parent = parentWooId ?? 0
                // ?????????: slug = Slugify(category.Name)
            };

            // 1) ?? ?? ??? WooCommerceId - ???? update
            if (category.WooCommerceId.HasValue)
            {
                var updatedId = await TryUpdateCategoryAsync(baseUrl, category.WooCommerceId.Value, wooCatData, httpClient, cancelToken);
                if (updatedId.HasValue) return updatedId.Value;
            }

            // 2) ???? ???? create
            var createUrl = $"{baseUrl}/products/categories";
            var createJson = JsonSerializer.Serialize(wooCatData);
            using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

            var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);

            // Success
            if (createResponse.IsSuccessStatusCode)
            {
                var created = await JsonSerializer.DeserializeAsync<WooCommerceCategoryResponse>(
                    await createResponse.Content.ReadAsStreamAsync(cancelToken),
                    cancellationToken: cancelToken);
                return created?.id;
            }

            // Error -> read body
            var errorBody = await createResponse.Content.ReadAsStringAsync(cancelToken);

            // term_exists -> return resource_id (and optionally update it)
            var wooErr = TryDeserialize<WooErrorResponse>(errorBody);
            if (wooErr?.code == "term_exists" && wooErr.data?.resource_id is int existingId)
            {
                // ?????????: ????? ??existing category ?? description ???'
                var updatedId = await TryUpdateCategoryAsync(baseUrl, existingId, wooCatData, httpClient, cancelToken);
                return updatedId ?? existingId;
            }

            throw new Exception($"WooCommerce API error ({createResponse.StatusCode}): {errorBody}");
        }

        private async Task<int?> TryUpdateCategoryAsync(
            string baseUrl,
            int wooCategoryId,
            object wooCatData,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/categories/{wooCategoryId}";
            var updateJson = JsonSerializer.Serialize(wooCatData);
            using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

            var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return null;

            var updated = await JsonSerializer.DeserializeAsync<WooCommerceCategoryResponse>(
                await updateResponse.Content.ReadAsStreamAsync(cancelToken),
                cancellationToken: cancelToken);

            return updated?.id;
        }

        private static T? TryDeserialize<T>(string json)
        {
            try { return JsonSerializer.Deserialize<T>(json); }
            catch { return default; }
        }

        private async Task<List<WooCommerceSyncResult>> SyncProductsAsync(
            string baseUrl,
            int siteId,
            List<int>? productIds,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var results = new List<WooCommerceSyncResult>();

            // Get products to sync - load individually to ensure all relationships are included
            List<Product> productsToSync;
            
            if (productIds != null && productIds.Any())
            {
                // Load specific products
                productsToSync = new List<Product>();
                foreach (var productId in productIds)
                {
                    var product = await _productStorage.GetProductAsync(productId, cancelToken);
                    if (product != null && (product.Sites.Any(s => s.Id == siteId) || product.Sites.Count == 0))
                    {
                        productsToSync.Add(product);
                    }
                }
            }
            else
            {
                // Load all products for the site
                var filter = new ProductFilter { SiteId = siteId };
                var products = await _productStorage.GetProductsAsync(
                    filter,
                    new PagingExDto { Skip = 0, Take = 10000, IncludeTotal = false },
                    cancelToken);
                productsToSync = products.Items.Where(p => p.Sites.Any(s => s.Id == siteId) || p.Sites.Count == 0).ToList();
            }

            // Sync products in batches of 5
            const int batchSize = 5;
            for (int i = 0; i < productsToSync.Count; i += batchSize)
            {
                var batch = productsToSync.Skip(i).Take(batchSize).ToList();
                var batchTasks = batch.Select(p => SyncProductAsync(baseUrl, p, categoryMap, httpClient, cancelToken));
                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);
            }

            return results;
        }

        private async Task<WooCommerceSyncResult> SyncProductAsync(
            string baseUrl,
            Product product,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            try
            {
                // Map stock status
                var stockStatus = "instock";
                if (product.StockStatus?.Name == "out_of_stock")
                    stockStatus = "outofstock";
                else if (product.StockStatus?.Name == "on_backorder")
                    stockStatus = "onbackorder";

                // Map visibility
                var catalogVisibility = "visible";
                if (product.Visibility?.Name == "hidden")
                    catalogVisibility = "hidden";
                else if (product.Visibility?.Name == "private")
                    catalogVisibility = "catalog";

                // Map shipping class
                var shippingClass = "";
                if (product.ShippingClass?.Name == "heavy")
                    shippingClass = "heavy";
                else if (product.ShippingClass?.Name == "fragile")
                    shippingClass = "fragile";

                // Map categories
                var allCategoryIds = product.ProductCategories?
                    .Select(pc => pc.CategoryId)
                    .Where(id => categoryMap.ContainsKey(id))
                    .Select(id => new { id = categoryMap[id] })
                    .Cast<object>()
                    .ToList() ?? new List<object>();

                // Map images
                var images = product.ProductImages?
    .OrderBy(pi => pi.SortOrder)
    .Where(pi => IsPublicImageUrl(pi.Url))
    .Select((pi, index) => new { src = pi.Url, position = index })
    .Cast<object>()
    .ToList() ?? new List<object>();

                // Map tags
                var tags = product.Tags?
                    .Select(t => new { name = t.Name })
                    .Cast<object>()
                    .ToList() ?? new List<object>();

                // Build meta data
                var metaData = new List<object>();
                if (!string.IsNullOrEmpty(product.Brand?.Name))
                    metaData.Add(new { key = "_brand", value = product.Brand.Name });
                if (!string.IsNullOrEmpty(product.Supplier?.Name))
                    metaData.Add(new { key = "_supplier", value = product.Supplier.Name });
                metaData.Add(new { key = "_is_kosher", value = product.IsKosher == true ? "yes" : "no" });
                if (product.CostPrice.HasValue)
                    metaData.Add(new { key = "_cost_price", value = product.CostPrice.Value.ToString() });
                if (!string.IsNullOrEmpty(product.SeoTitle))
                    metaData.Add(new { key = "_yoast_wpseo_title", value = product.SeoTitle });
                if (!string.IsNullOrEmpty(product.SeoDescription))
                    metaData.Add(new { key = "_yoast_wpseo_metadesc", value = product.SeoDescription });

                // Weighted product fields
                if (product.IsWeighted == true)
                {
                    metaData.Add(new { key = "ocwsu_weightable", value = "yes" });
                    var setupType = product.SetupType?.Name ?? "";
                    metaData.Add(new { key = "ocwsu_sold_by_units_", value = (setupType == "by_unit" || setupType == "by_unit_and_weight") ? "yes" : "no" });
                    metaData.Add(new { key = "ocwsu_sold_by_weight_", value = (setupType == "by_weight" || setupType == "by_unit_and_weight") ? "yes" : "no" });

                    if (product.WeightConfig != null)
                    {
                        // Load WeightConfig with Unit and UnitWeightMode if not already loaded
                        var weightConfig = product.WeightConfig;
                        if (weightConfig.UnitId.HasValue && weightConfig.Unit == null)
                        {
                            // Unit not loaded, skip it for now (would need to query separately)
                        }
                        else if (weightConfig.Unit != null && !string.IsNullOrEmpty(weightConfig.Unit.Name))
                        {
                            metaData.Add(new { key = "ocwsu_product_weight_units_", value = weightConfig.Unit.Name });
                        }

                        metaData.Add(new { key = "ocwsu_display_price_per_100g_", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        
                        if (!string.IsNullOrEmpty(weightConfig.StartWeight))
                            metaData.Add(new { key = "ocwsu_min_weight_", value = weightConfig.StartWeight });
                        if (!string.IsNullOrEmpty(weightConfig.Step))
                            metaData.Add(new { key = "ocwsu_weight_step_", value = weightConfig.Step });
                        
                        if (weightConfig.UnitWeightModeId.HasValue && weightConfig.UnitWeightMode == null)
                        {
                            // UnitWeightMode not loaded, skip it for now
                        }
                        else if (weightConfig.UnitWeightMode != null && !string.IsNullOrEmpty(weightConfig.UnitWeightMode.Name))
                        {
                            metaData.Add(new { key = "ocwsu_unit_weight_type_", value = weightConfig.UnitWeightMode.Name });
                        }
                        
                        if (!string.IsNullOrEmpty(weightConfig.UnitWeight))
                            metaData.Add(new { key = "ocwsu_unit_weight_", value = weightConfig.UnitWeight });
                        if (!string.IsNullOrEmpty(weightConfig.WeightOptions))
                            metaData.Add(new { key = "ocwsu_unit_weight_options_", value = weightConfig.WeightOptions });
                        metaData.Add(new { key = "ocwsu_get_weight_from_variation_", value = weightConfig.WeightByVariant == true ? "yes" : "no" });
                    }
                }

                var wooProduct = new Dictionary<string, object>
                {
                    ["name"] = product.Name,
                    ["type"] = (product.ProductVariants != null && product.ProductVariants.Any(v => !v.IsDeleted)) ? "variable" : "simple",
                    ["description"] = product.LongDescription ?? product.ShortDescription ?? "",
                    ["short_description"] = product.ShortDescription ?? "",
                    ["sku"] = product.Sku ?? "",
                    ["catalog_visibility"] = catalogVisibility,
                    ["weight"] = product.Weight?.ToString() ?? "",
                    ["shipping_class"] = shippingClass,
                    //["images"] = images,
                    ["categories"] = allCategoryIds,
                    ["tags"] = tags,
                    ["status"] = product.Status?.Name == "published" ? "publish" : "draft",
                    ["meta_data"] = metaData
                };

                if (images.Count > 0)
                    wooProduct["images"] = images;

                // For simple products, add pricing and stock
                if (product.ProductVariants == null || !product.ProductVariants.Any(v => !v.IsDeleted))
                {
                    wooProduct["regular_price"] = product.Price?.ToString() ?? "0";
                    wooProduct["sale_price"] = product.SalePrice?.ToString() ?? "";
                    wooProduct["date_on_sale_from"] = product.SalePriceStartDate?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "";
                    wooProduct["date_on_sale_to"] = product.SalePriceEndDate?.ToString("yyyy-MM-ddTHH:mm:ss") ?? "";
                    wooProduct["manage_stock"] = product.StockManagementType?.Name == "quantity";
                    wooProduct["stock_quantity"] = product.StockQuantity ?? 0;
                    wooProduct["stock_status"] = stockStatus;
                    wooProduct["backorders"] = product.StockStatus?.Name == "on_backorder" ? "yes" : "no";
                }
                else
                {
                    // For variable products, add attributes
                    var attributes = product.ProductOptions?
                        .Where(po => !po.IsDeleted)
                        .Select((option, index) => new
                        {
                            id = 0,
                            name = option.Name,
                            position = index,
                            visible = true,
                            variation = true,
                            options = option.ProductOptionValues?.Select(pov => pov.Value).ToList() ?? new List<string>()
                        })
                        .Cast<object>()
                        .ToList() ?? new List<object>();

                    wooProduct["attributes"] = attributes;
                }

                // Create or update product
                int? wooCommerceId = null;
                string action = "created";

                if (product.WooCommerceId.HasValue)
                {
                    // Try to update
                    var updateUrl = $"{baseUrl}/products/{product.WooCommerceId.Value}";
                    var updateJson = JsonSerializer.Serialize(wooProduct);
                    var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                    var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
                    if (updateResponse.IsSuccessStatusCode)
                    {
                        var updated = await JsonSerializer.DeserializeAsync<WooCommerceProductResponse>(
                            await updateResponse.Content.ReadAsStreamAsync(cancelToken),
                            cancellationToken: cancelToken);
                        wooCommerceId = updated?.id;
                        action = "updated";
                    }
                    else
                    {
                        // If update fails, try to create
                        var errorContent = await updateResponse.Content.ReadAsStringAsync(cancelToken);
                        _logger.LogWarning("Failed to update product {ProductId} in WooCommerce: {Error}", product.Id, errorContent);
                    }
                }

                if (!wooCommerceId.HasValue)
                {
                    // Create new product
                    var createUrl = $"{baseUrl}/products";
                    var createJson = JsonSerializer.Serialize(wooProduct);
                    var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

                    try
                    {

                        var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
                        if (!createResponse.IsSuccessStatusCode)
                        {
                            var errorContent = await createResponse.Content.ReadAsStringAsync(cancelToken);
                            throw new Exception($"WooCommerce API error ({createResponse.StatusCode}): {errorContent}");
                        }

                        var created = await JsonSerializer.DeserializeAsync<WooCommerceProductResponse>(
                            await createResponse.Content.ReadAsStreamAsync(cancelToken),
                            cancellationToken: cancelToken);
                        wooCommerceId = created?.id;
                    }
                    catch(Exception e)
                    {
                        Console.Write(e);
                    }
                }

                // Update product with WooCommerce ID
                if (wooCommerceId.HasValue && product.WooCommerceId != wooCommerceId.Value)
                {
                    await _productStorage.UpdateProductWooCommerceIdAsync(product.Id, wooCommerceId.Value, cancelToken);
                }

                // Sync variations for variable products
                if (wooCommerceId.HasValue && product.ProductVariants != null && product.ProductVariants.Any(v => !v.IsDeleted))
                {
                    await SyncProductVariantsAsync(baseUrl, wooCommerceId.Value, product, httpClient, cancelToken);
                }

                return new WooCommerceSyncResult
                {
                    Success = true,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    WooCommerceId = wooCommerceId,
                    Action = action
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync product {ProductId}", product.Id);
                return new WooCommerceSyncResult
                {
                    Success = false,
                    ProductId = product.Id,
                    ProductName = product.Name,
                    Error = ex.Message
                };
            }
        }

        private async Task SyncProductVariantsAsync(
            string baseUrl,
            int wooProductId,
            Product product,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var variants = product.ProductVariants?.Where(v => !v.IsDeleted).ToList() ?? new List<ProductVariant>();

            foreach (var variant in variants)
            {
                try
                {
                    var variantStockStatus = (variant.StockQuantity ?? 0) > 0 ? "instock" : "outofstock";

                    var variantOptionValues = variant.ProductVariantOptionValues?
                        .ToDictionary(pvov => pvov.OptionName, pvov => pvov.OptionValue) ?? new Dictionary<string, string>();

                    var wooVariation = new Dictionary<string, object>
                    {
                        ["regular_price"] = variant.Price?.ToString() ?? product.Price?.ToString() ?? "0",
                        ["sale_price"] = variant.SalePrice?.ToString() ?? "",
                        ["sku"] = variant.Sku ?? "",
                        ["manage_stock"] = true,
                        ["stock_quantity"] = variant.StockQuantity ?? 0,
                        ["stock_status"] = variantStockStatus,
                        ["weight"] = variant.Weight?.ToString() ?? "",
                        ["attributes"] = variantOptionValues.Select(kvp => new { name = kvp.Key, option = kvp.Value }).ToList()
                    };

                    if (!string.IsNullOrEmpty(variant.ImageUrl))
                    {
                        wooVariation["image"] = new { src = variant.ImageUrl };
                    }

                    int? wooVariationId = null;

                    if (variant.WooCommerceVariationId.HasValue)
                    {
                        // Try to update
                        var updateUrl = $"{baseUrl}/products/{wooProductId}/variations/{variant.WooCommerceVariationId.Value}";
                        var updateJson = JsonSerializer.Serialize(wooVariation);
                        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");

                        var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
                        if (updateResponse.IsSuccessStatusCode)
                        {
                            var updated = await JsonSerializer.DeserializeAsync<WooCommerceVariationResponse>(
                                await updateResponse.Content.ReadAsStreamAsync(cancelToken),
                                cancellationToken: cancelToken);
                            wooVariationId = updated?.id;
                        }
                    }

                    if (!wooVariationId.HasValue)
                    {
                        // Create new variation
                        var createUrl = $"{baseUrl}/products/{wooProductId}/variations";
                        var createJson = JsonSerializer.Serialize(wooVariation);
                        var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");

                        var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
                        if (createResponse.IsSuccessStatusCode)
                        {
                            var created = await JsonSerializer.DeserializeAsync<WooCommerceVariationResponse>(
                                await createResponse.Content.ReadAsStreamAsync(cancelToken),
                                cancellationToken: cancelToken);
                            wooVariationId = created?.id;
                        }
                        else
                        {
                            var errorContent = await createResponse.Content.ReadAsStringAsync(cancelToken);
                            _logger.LogWarning("Failed to create variation for product {ProductId}: {Error}", product.Id, errorContent);
                        }
                    }

                    if (wooVariationId.HasValue && variant.WooCommerceVariationId != wooVariationId.Value)
                    {
                        await _productStorage.UpdateProductVariantWooCommerceIdAsync(variant.Id, wooVariationId.Value, cancelToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync variation {VariantId} for product {ProductId}", variant.Id, product.Id);
                }
            }
        }

        private static bool IsPublicImageUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

            var host = uri.Host.ToLowerInvariant();
            if (host == "localhost" || host == "127.0.0.1" || host == "::1") return false;

            // ?????????: ????? ?? private ranges ??? ????
            return true;
        }

        // Helper classes for WooCommerce API responses
        private class WooCommerceCategoryResponse
        {
            public int id { get; set; }
        }

        private class WooCommerceProductResponse
        {
            public int id { get; set; }
        }

        private class WooCommerceVariationResponse
        {
            public int id { get; set; }
        }

        public class WooErrorResponse
        {
            public string? code { get; set; }
            public string? message { get; set; }
            public WooErrorData? data { get; set; }

            public class WooErrorData
            {
                public int? status { get; set; }
                public int? resource_id { get; set; }
            }
        }
    }
}

