using AutoMapper;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Attribute = George.DB.Attribute;

namespace George.Services
{
    public class WooCommerceService : ServiceBase
    {
        private readonly SiteStorage _siteStorage;
        private readonly CategoryStorage _categoryStorage;
        private readonly ProductStorage _productStorage;
        private readonly AttributeStorage _attributeStorage;
        private readonly IHttpClientFactory _httpClientFactory;

        public WooCommerceService(
            ILogger<WooCommerceService> logger,
            IMapper mapper,
            CacheManager cache,
            SiteStorage siteStorage,
            CategoryStorage categoryStorage,
            ProductStorage productStorage,
            AttributeStorage attributeStorage,
            IHttpClientFactory httpClientFactory
        ) : base(logger, mapper, cache)
        {
            _siteStorage = siteStorage;
            _categoryStorage = categoryStorage;
            _productStorage = productStorage;
            _attributeStorage = attributeStorage;
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

        /// <summary>
        /// Syncs a single category to WooCommerce for a specific site
        /// </summary>
        public async Task<IApiResponse<WooCommerceCategorySyncRes>> SyncCategoryToWooCommerceAsync(
            int categoryId,
            int siteId,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceCategorySyncRes>
            {
                Data = new WooCommerceCategorySyncRes()
            };

            try
            {
                // Get category
                var category = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
                if (category == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Category not found");
                }

                // Get site
                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
                if (site == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                }

                // Check if WooCommerce is enabled for this site
                if (site.WooCommerceEnabled != true ||
                    string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce is not enabled or configured for this site");
                }

                // Setup WooCommerce API client
                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                // Get parent WooCommerce ID if category has a parent
                int? parentWooId = null;
                if (category.ParentCategoryId.HasValue)
                {
                    var parentCategory = await _categoryStorage.GetCategoryAsync(category.ParentCategoryId.Value, cancelToken);
                    if (parentCategory?.WooCommerceId.HasValue == true)
                    {
                        parentWooId = parentCategory.WooCommerceId.Value;
                    }
                }

                // Sync the category
                var wooCatId = await SyncCategoryAsync(baseUrl, category, parentWooId, httpClient, cancelToken);

                if (wooCatId.HasValue)
                {
                    // Update category with WooCommerce ID
                    await _categoryStorage.UpdateCategoryWooCommerceIdAsync(categoryId, wooCatId.Value, cancelToken);

                    response.Data.CategoryId = categoryId;
                    response.Data.WooCommerceId = wooCatId.Value;
                    response.Data.Success = true;
                    response.Data.Message = "Category synced successfully";
                }
                else
                {
                    response.Data.Success = false;
                    response.Data.Message = "Failed to sync category to WooCommerce";
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing category {CategoryId} to WooCommerce for site {SiteId}", categoryId, siteId);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        /// <summary>
        /// Syncs a single site attribute to WooCommerce product attributes and terms for that site.
        /// </summary>
        public async Task<IApiResponse<WooCommerceAttributeSyncRes>> SyncAttributeToWooCommerceAsync(
            int attributeId,
            int siteId,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<WooCommerceAttributeSyncRes>
            {
                Data = new WooCommerceAttributeSyncRes()
            };

            try
            {
                var attribute = await _attributeStorage.GetAttributeAsync(attributeId, cancelToken);
                if (attribute == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Attribute not found");
                }

                if (attribute.SiteId != siteId)
                {
                    return CreateResponse(response, StatusCode.InvalidRequest, "Attribute does not belong to this site");
                }

                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
                if (site == null)
                {
                    return CreateResponse(response, StatusCode.ItemNotFound, "Site not found");
                }

                if (site.WooCommerceEnabled != true ||
                    string.IsNullOrEmpty(site.WooCommerceUrl) ||
                    string.IsNullOrEmpty(site.WooCommerceKey) ||
                    string.IsNullOrEmpty(site.WooCommerceSecret))
                {
                    return CreateResponse(response, StatusCode.InvalidRequest,
                        "WooCommerce is not enabled or configured for this site");
                }

                var baseUrl = $"{site.WooCommerceUrl.TrimEnd('/')}/wp-json/wc/v3";
                var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{site.WooCommerceKey}:{site.WooCommerceSecret}"));

                using var httpClient = _httpClientFactory.CreateClient();
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Add("Authorization", $"Basic {auth}");

                var wooAttrId = await SyncAttributeAsync(baseUrl, attribute, httpClient, cancelToken);

                if (wooAttrId.HasValue)
                {
                    await _attributeStorage.UpdateAttributeWooCommerceIdAsync(attributeId, wooAttrId.Value, cancelToken);
                    response.Data.AttributeId = attributeId;
                    response.Data.WooCommerceId = wooAttrId.Value;
                    response.Data.Success = true;
                    response.Data.Message = "Attribute synced successfully";
                }
                else
                {
                    response.Data.Success = false;
                    response.Data.Message = "Failed to sync attribute to WooCommerce";
                }

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing attribute {AttributeId} to WooCommerce for site {SiteId}", attributeId, siteId);
                return CreateResponse(response, StatusCode.UnknownError, ex.Message);
            }
        }

        private static string SlugifyAttributeName(string name)
        {
            var slug = Regex.Replace(name ?? "", @"[^a-zA-Z0-9]+", "_").ToLowerInvariant().Trim('_');
            return string.IsNullOrEmpty(slug) ? "attr" : slug;
        }

        /// <summary>
        /// Finds an existing WooCommerce global attribute by name or slug.
        /// Returns the attribute ID if found, null otherwise.
        /// </summary>
        private async Task<int?> FindExistingAttributeAsync(string baseUrl, string attributeName, HttpClient httpClient, CancellationToken cancelToken)
        {
            try
            {
                var slug = "pa_" + SlugifyAttributeName(attributeName);
                // Try to find by slug first (more reliable)
                var searchUrl = $"{baseUrl}/products/attributes?slug={Uri.EscapeDataString(slug)}";
                var searchResponse = await httpClient.GetAsync(searchUrl, cancelToken);
                
                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchBody = await searchResponse.Content.ReadAsStringAsync(cancelToken);
                    var attributes = TryDeserialize<List<WooCommerceAttributeResponse>>(searchBody);
                    if (attributes != null && attributes.Count > 0)
                    {
                        // Check if name matches (slug might match but name might differ)
                        var matchingAttr = attributes.FirstOrDefault(a => 
                            string.Equals(a.name, attributeName, StringComparison.OrdinalIgnoreCase));
                        if (matchingAttr != null)
                        {
                            return matchingAttr.id;
                        }
                    }
                }

                // Also try searching by name (in case slug doesn't match exactly)
                var nameSearchUrl = $"{baseUrl}/products/attributes";
                var nameSearchResponse = await httpClient.GetAsync(nameSearchUrl, cancelToken);
                
                if (nameSearchResponse.IsSuccessStatusCode)
                {
                    var nameSearchBody = await nameSearchResponse.Content.ReadAsStringAsync(cancelToken);
                    var allAttributes = TryDeserialize<List<WooCommerceAttributeResponse>>(nameSearchBody);
                    if (allAttributes != null)
                    {
                        var matchingByName = allAttributes.FirstOrDefault(a => 
                            string.Equals(a.name, attributeName, StringComparison.OrdinalIgnoreCase));
                        if (matchingByName != null)
                        {
                            return matchingByName.id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching for existing WooCommerce attribute with name {AttributeName}", attributeName);
            }

            return null;
        }

        private async Task<int?> SyncAttributeAsync(string baseUrl, Attribute attribute, HttpClient httpClient, CancellationToken cancelToken)
        {
            var slug = "pa_" + SlugifyAttributeName(attribute.Name);
            var wooAttrData = new { name = attribute.Name, slug, type = "select", order_by = "menu_order", has_archives = false };

            // First, check if we have a stored WooCommerceId and try to update
            if (attribute.WooCommerceId.HasValue)
            {
                var updatedId = await TryUpdateProductAttributeAsync(baseUrl, attribute.WooCommerceId.Value, wooAttrData, httpClient, cancelToken);
                if (updatedId.HasValue)
                {
                    await SyncAttributeTermsAsync(baseUrl, updatedId.Value, attribute, httpClient, cancelToken);
                    return updatedId.Value;
                }
            }

            // If update failed or no WooCommerceId, try to find existing attribute by name/slug
            var existingId = await FindExistingAttributeAsync(baseUrl, attribute.Name, httpClient, cancelToken);
            if (existingId.HasValue)
            {
                // Found existing attribute, sync terms and return the ID
                await SyncAttributeTermsAsync(baseUrl, existingId.Value, attribute, httpClient, cancelToken);
                return existingId.Value;
            }

            // No existing attribute found, create a new one
            var createUrl = $"{baseUrl}/products/attributes";
            var createJson = JsonSerializer.Serialize(wooAttrData);
            using var createContent = new StringContent(createJson, Encoding.UTF8, "application/json");
            var createResponse = await httpClient.PostAsync(createUrl, createContent, cancelToken);
            var responseBody = await createResponse.Content.ReadAsStringAsync(cancelToken);

            if (createResponse.IsSuccessStatusCode)
            {
                var created = TryDeserializeFromResponse<WooCommerceAttributeResponse>(responseBody, createUrl, "POST");
                if (created?.id is int id)
                {
                    await SyncAttributeTermsAsync(baseUrl, id, attribute, httpClient, cancelToken);
                    return id;
                }
            }

            throw new Exception($"WooCommerce API error ({createResponse.StatusCode}): {responseBody}");
        }

        private async Task<int?> TryUpdateProductAttributeAsync(string baseUrl, int wooAttrId, object wooAttrData, HttpClient httpClient, CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/attributes/{wooAttrId}";
            var updateJson = JsonSerializer.Serialize(wooAttrData);
            using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
            var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
            var responseBody = await updateResponse.Content.ReadAsStringAsync(cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return null;

            var updated = TryDeserializeFromResponse<WooCommerceAttributeResponse>(responseBody, updateUrl, "PUT");
            return updated?.id;
        }

        private async Task SyncAttributeTermsAsync(string baseUrl, int wooAttrId, Attribute attribute, HttpClient httpClient, CancellationToken cancelToken)
        {
            var values = attribute.AttributeValues?.Select(av => av.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? new List<string>();
            foreach (var value in values)
            {
                var termUrl = $"{baseUrl}/products/attributes/{wooAttrId}/terms";
                var termBody = JsonSerializer.Serialize(new { name = value.Trim() });
                using var termContent = new StringContent(termBody, Encoding.UTF8, "application/json");
                var termRes = await httpClient.PostAsync(termUrl, termContent, cancelToken);
                if (!termRes.IsSuccessStatusCode)
                {
                    var err = await termRes.Content.ReadAsStringAsync(cancelToken);
                    _logger.LogWarning("Failed to create attribute term {Value} for WooCommerce attribute {WooAttrId}: {Error}", value, wooAttrId, err);
                }
            }
        }

        /// <summary>
        /// Ensures a global attribute exists in the database and is synced to WooCommerce.
        /// Returns the WooCommerce attribute ID.
        /// </summary>
        private async Task<int?> EnsureGlobalAttributeAsync(
            string baseUrl,
            string attributeName,
            List<string> attributeValues,
            int siteId,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            // First, try to find existing Attribute in DB by name and siteId
            var filter = new AttributeFilter { Name = attributeName };
            var paging = new PagingExDto { Skip = 0, Take = 10, IncludeTotal = false };
            var existingAttributes = await _attributeStorage.GetAttributesAsync(filter, paging, cancelToken);
            var existingAttribute = existingAttributes.Items.FirstOrDefault(a => 
                a.SiteId == siteId && 
                string.Equals(a.Name, attributeName, StringComparison.OrdinalIgnoreCase) &&
                !a.IsDeleted);

            Attribute attribute;
            if (existingAttribute != null)
            {
                attribute = existingAttribute;
            }
            else
            {
                // Create new Attribute in DB
                attribute = await _attributeStorage.CreateAttributeAsync(
                    new Attribute
                    {
                        Name = attributeName,
                        SiteId = siteId,
                        CreationTime = DateTime.UtcNow,
                        GuidId = Guid.NewGuid()
                    },
                    attributeValues,
                    cancelToken);
            }

            // Sync to WooCommerce
            var wooCommerceId = await SyncAttributeAsync(baseUrl, attribute, httpClient, cancelToken);
            
            // Update Attribute.WooCommerceId in DB if it changed
            if (wooCommerceId.HasValue && attribute.WooCommerceId != wooCommerceId.Value)
            {
                await _attributeStorage.UpdateAttributeWooCommerceIdAsync(attribute.Id, wooCommerceId.Value, cancelToken);
            }

            return wooCommerceId;
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
            var responseBody = await createResponse.Content.ReadAsStringAsync(cancelToken);

            // Success
            if (createResponse.IsSuccessStatusCode)
            {
                var created = TryDeserializeFromResponse<WooCommerceCategoryResponse>(responseBody, createUrl, "POST");
                return created?.id;
            }

            // Error -> read body (already in responseBody)
            var errorBody = responseBody;

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
            var responseBody = await updateResponse.Content.ReadAsStringAsync(cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return null;

            var updated = TryDeserializeFromResponse<WooCommerceCategoryResponse>(responseBody, updateUrl, "PUT");
            return updated?.id;
        }

        /// <summary>
        /// Deserializes JSON from WooCommerce API response. If the response is HTML (e.g. error page, redirect)
        /// instead of JSON, throws a clear exception to avoid JsonException on '&lt;' invalid start.
        /// </summary>
        private static T? TryDeserializeFromResponse<T>(string responseBody, string requestUrl, string method)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return default;
            var trimmed = responseBody.TrimStart();
            if (trimmed.StartsWith("<"))
                throw new Exception($"WooCommerce returned HTML instead of JSON for {method} {requestUrl}. Check that the WooCommerce URL points to the site root and the REST API is enabled. Response starts with: {(responseBody.Length > 120 ? responseBody.Substring(0, 120) + "..." : responseBody)}");
            try
            {
                return JsonSerializer.Deserialize<T>(responseBody);
            }
            catch (JsonException ex)
            {
                throw new Exception($"WooCommerce response is not valid JSON for {method} {requestUrl}. {ex.Message}. Response starts with: {(responseBody.Length > 150 ? responseBody.Substring(0, 150) + "..." : responseBody)}", ex);
            }
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


            //productsToSync = productsToSync.Where(x => x.Id == 1255).ToList();

            // Sync products in batches of 5
            const int batchSize = 5;
            for (int i = 0; i < productsToSync.Count; i += batchSize)
            {
                var batch = productsToSync.Skip(i).Take(batchSize).ToList();
                var batchTasks = batch.Select(p => SyncProductAsync(baseUrl, siteId, p, categoryMap, httpClient, cancelToken));
                var batchResults = await Task.WhenAll(batchTasks);
                results.AddRange(batchResults);
            }

            return results;
        }

        private async Task<WooCommerceSyncResult> SyncProductAsync(
            string baseUrl,
            int siteId,
            Product product,
            Dictionary<int, int> categoryMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            try
            {
                // Map stock status
                var stockStatus = "instock";
                if (product.StockStatus?.Name == "out_of_stock" || product.Status?.Name == "outOfStock")
                    stockStatus = "outofstock";
                else if (product.StockStatus?.Name == "on_backorder")
                    stockStatus = "onbackorder";

                // Map visibility
                var catalogVisibility = "visible";
                if (product.Visibility?.Name == "hidden" || product.Status?.Name == "hidden")
                    catalogVisibility = "hidden";
                else if (product.Visibility?.Name == "outOfStock")
                    catalogVisibility = "visible"; // Still visible even if out of stock

                // Map WooCommerce status
                var wooStatus = "publish";
                if (product.Status?.Name == "hidden")
                    wooStatus = "private";
                else if (product.Status?.Name == "outOfStock")
                    wooStatus = "publish";
                else if (product.Status?.Name == "active")
                    wooStatus = "publish";
                else if (product.Status?.Name == "draft")
                    wooStatus = "draft";
                else if (product.Status?.Name == "archived")
                    wooStatus = "private";

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

                // Weighted product fields - "זה מוצר שקיל". Keys must match WordPress admin POST (post.php): leading _ and no trailing _.
                var isWeighted = product.IsWeighted == true;
                var weighableValue = isWeighted ? "yes" : "no";
                metaData.Add(new { key = "_ocwsu_weighable", value = weighableValue });
                metaData.Add(new { key = "ocwsu_weighable_", value = weighableValue });
                metaData.Add(new { key = "ocwsu_weightable", value = weighableValue });

                if (isWeighted)
                {
                    var setupType = product.SetupType?.Name ?? "";
                    var soldByUnits = (setupType == "by_unit" || setupType == "by_unit_and_weight") ? "yes" : "no";
                    var soldByWeight = (setupType == "by_weight" || setupType == "by_unit_and_weight") ? "yes" : "no";
                    metaData.Add(new { key = "_ocwsu_sold_by_units", value = soldByUnits });
                    metaData.Add(new { key = "ocwsu_sold_by_units_", value = soldByUnits });
                    metaData.Add(new { key = "_ocwsu_sold_by_weight", value = soldByWeight });
                    metaData.Add(new { key = "ocwsu_sold_by_weight_", value = soldByWeight });

                    if (product.WeightConfig != null)
                    {
                        var weightConfig = product.WeightConfig;
                        metaData.Add(new { key = "_ocwsu_product_weight_units", value = weightConfig.Unit?.Name ?? "" });
                        metaData.Add(new { key = "ocwsu_product_weight_units_", value = weightConfig.Unit?.Name ?? "" });
                        metaData.Add(new { key = "_ocwsu_display_price_per_100g", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        metaData.Add(new { key = "ocwsu_display_price_per_100g_", value = weightConfig.ShowPricePer100g == true ? "yes" : "no" });
                        metaData.Add(new { key = "_ocwsu_min_weight", value = weightConfig.StartWeight ?? "" });
                        metaData.Add(new { key = "ocwsu_min_weight_", value = weightConfig.StartWeight ?? "" });
                        metaData.Add(new { key = "_ocwsu_weight_step", value = weightConfig.Step ?? "" });
                        metaData.Add(new { key = "ocwsu_weight_step_", value = weightConfig.Step ?? "" });
                        metaData.Add(new { key = "_ocwsu_unit_weight_type", value = weightConfig.UnitWeightMode?.Name ?? "" });
                        metaData.Add(new { key = "ocwsu_unit_weight_type_", value = weightConfig.UnitWeightMode?.Name ?? "" });
                        metaData.Add(new { key = "_ocwsu_unit_weight", value = weightConfig.UnitWeight ?? "" });
                        metaData.Add(new { key = "ocwsu_unit_weight_", value = weightConfig.UnitWeight ?? "" });
                        metaData.Add(new { key = "_ocwsu_unit_weight_options", value = weightConfig.WeightOptions ?? "" });
                        metaData.Add(new { key = "ocwsu_unit_weight_options_", value = weightConfig.WeightOptions ?? "" });
                        var getWeightFromVariation = weightConfig.WeightByVariant == true ? "yes" : "no";
                        metaData.Add(new { key = "_ocwsu_get_weight_from_variation", value = getWeightFromVariation });
                        metaData.Add(new { key = "ocwsu_get_weight_from_variation_", value = getWeightFromVariation });
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
                    ["status"] = wooStatus,
                    ["meta_data"] = metaData
                };

                if (images.Count > 0)
                    wooProduct["images"] = images;

                // For variable products, ensure global attributes exist and use their IDs
                var attributeMap = new Dictionary<string, int?>(); // Maps attribute name to WooCommerce ID

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
                    // For variable products, ensure global attributes exist and use their IDs
                    
                    if (product.ProductOptions != null)
                    {
                        foreach (var option in product.ProductOptions.Where(po => !po.IsDeleted))
                        {
                            var attributeValues = option.ProductOptionValues?
                                .Select(pov => pov.Value)
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .ToList() ?? new List<string>();

                            if (attributeValues.Any())
                            {
                                // Ensure global attribute exists in DB and WooCommerce
                                var wooAttrId = await EnsureGlobalAttributeAsync(
                                    baseUrl,
                                    option.Name,
                                    attributeValues,
                                    siteId,
                                    httpClient,
                                    cancelToken);
                                
                                if (wooAttrId.HasValue)
                                {
                                    attributeMap[option.Name] = wooAttrId.Value;
                                }
                            }
                        }
                    }

                    // Build attributes array using global attribute IDs
                    var attributes = product.ProductOptions?
                        .Where(po => !po.IsDeleted && attributeMap.ContainsKey(po.Name))
                        .Select((option, index) => new
                        {
                            id = attributeMap[option.Name]!.Value,
                            name = option.Name,
                            position = index,
                            visible = true,
                            variation = true,
                            options = option.ProductOptionValues?.Select(pov => pov.Value).Where(v => !string.IsNullOrWhiteSpace(v)).ToList() ?? new List<string>()
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
                    await SyncProductVariantsAsync(baseUrl, wooCommerceId.Value, product, attributeMap, httpClient, cancelToken);
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
            Dictionary<string, int?> attributeMap,
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

                    // Build variation attributes using global attribute IDs
                    var variationAttributes = variantOptionValues
                        .Where(kvp => attributeMap.ContainsKey(kvp.Key) && attributeMap[kvp.Key].HasValue)
                        .Select(kvp => new { id = attributeMap[kvp.Key]!.Value, option = kvp.Value })
                        .ToList();

                    var wooVariation = new Dictionary<string, object>
                    {
                        ["regular_price"] = variant.Price?.ToString() ?? product.Price?.ToString() ?? "0",
                        ["sale_price"] = variant.SalePrice?.ToString() ?? "",
                        ["sku"] = variant.Sku ?? "",
                        ["manage_stock"] = true,
                        ["stock_quantity"] = variant.StockQuantity ?? 0,
                        ["stock_status"] = variantStockStatus,
                        ["weight"] = variant.Weight?.ToString() ?? "",
                        ["attributes"] = variationAttributes
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

        private class WooCommerceAttributeResponse
        {
            public int id { get; set; }
            public string? name { get; set; }
            public string? slug { get; set; }
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

