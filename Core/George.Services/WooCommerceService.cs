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

                var (wooAttrId, _) = await SyncAttributeAsync(baseUrl, attribute, httpClient, cancelToken);

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
            name ??= "";

            // Keep ASCII letters/digits for "normal" names
            var ascii = Regex.Replace(name, @"[^a-zA-Z0-9]+", "_")
                .ToLowerInvariant()
                .Trim('_');

            if (!string.IsNullOrEmpty(ascii))
                return TruncateWooSlug(ascii);

            // Hebrew/Arabic/etc: generate stable slug from hash to avoid collisions
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(name.Trim()));
            var hash = BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();

            return TruncateWooSlug("a_" + hash.Substring(0, 24)); // total <= 28
        }

        private static string TruncateWooSlug(string slug)
        {
            // Woo attribute slug limit is ~28 chars
            return slug.Length <= 28 ? slug : slug.Substring(0, 28);
        }


        /// <summary>
        /// Finds an existing WooCommerce global attribute by name or slug.
        /// Returns (id, slug) if found; (null, null) otherwise. Slug is the actual WooCommerce taxonomy slug (e.g. pa_xxx).
        /// </summary>
        private async Task<(int? id, string? slug)> FindExistingAttributeAsync(string baseUrl, string attributeName, HttpClient httpClient, CancellationToken cancelToken)
        {
            try
            {
                var slug = SlugifyAttributeName(attributeName);
                var searchUrl = $"{baseUrl}/products/attributes?slug={Uri.EscapeDataString(slug)}";

                var searchResponse = await httpClient.GetAsync(searchUrl, cancelToken);
                
                if (searchResponse.IsSuccessStatusCode)
                {
                    var searchBody = await searchResponse.Content.ReadAsStringAsync(cancelToken);
                    var attributes = TryDeserialize<List<WooCommerceAttributeResponse>>(searchBody);
                    if (attributes != null && attributes.Count > 0)
                    {
                        var matchingAttr = attributes.FirstOrDefault(a => 
                            string.Equals(a.name, attributeName, StringComparison.OrdinalIgnoreCase));
                        if (matchingAttr != null)
                        {
                            return (matchingAttr.id, matchingAttr.slug ?? slug);
                        }
                    }
                }

                // Also try searching by name (Hebrew names don't slug well; WooCommerce may have different slug)
                var nameSearchUrl = $"{baseUrl}/products/attributes?per_page=100&page=1";

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
                            return (matchingByName.id, matchingByName.slug ?? slug);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching for existing WooCommerce attribute with name {AttributeName}", attributeName);
            }

            return (null, null);
        }

        /// <summary>
        /// Syncs an attribute to WooCommerce. Returns (id, slug) - slug is the actual WooCommerce taxonomy slug.
        /// </summary>
        private async Task<(int? id, string? slug)> SyncAttributeAsync(string baseUrl, Attribute attribute, HttpClient httpClient, CancellationToken cancelToken)
        {
            var slug = SlugifyAttributeName(attribute.Name);
            var wooAttrData = new { name = attribute.Name, slug, type = "select", order_by = "menu_order", has_archives = false };

            // First, check if we have a stored WooCommerceId and try to update
            if (attribute.WooCommerceId.HasValue)
            {
                var (updatedId, updatedSlug) = await TryUpdateProductAttributeAsync(baseUrl, attribute.WooCommerceId.Value, wooAttrData, httpClient, cancelToken);
                if (updatedId.HasValue)
                {
                    await SyncAttributeTermsAsync(baseUrl, updatedId.Value, attribute, httpClient, cancelToken);
                    return (updatedId.Value, updatedSlug ?? slug);
                }
            }

            // If update failed or no WooCommerceId, try to find existing attribute by name/slug
            var (existingId, existingSlug) = await FindExistingAttributeAsync(baseUrl, attribute.Name, httpClient, cancelToken);
            if (existingId.HasValue)
            {
                await SyncAttributeTermsAsync(baseUrl, existingId.Value, attribute, httpClient, cancelToken);
                return (existingId.Value, existingSlug ?? slug);
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
                    return (id, created.slug ?? slug);
                }
            }

            throw new Exception($"WooCommerce API error ({createResponse.StatusCode}): {responseBody}");
        }

        private async Task<(int? id, string? slug)> TryUpdateProductAttributeAsync(string baseUrl, int wooAttrId, object wooAttrData, HttpClient httpClient, CancellationToken cancelToken)
        {
            var updateUrl = $"{baseUrl}/products/attributes/{wooAttrId}";
            var updateJson = JsonSerializer.Serialize(wooAttrData);
            using var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
            var updateResponse = await httpClient.PutAsync(updateUrl, updateContent, cancelToken);
            var responseBody = await updateResponse.Content.ReadAsStringAsync(cancelToken);
            if (!updateResponse.IsSuccessStatusCode) return (null, null);

            var updated = TryDeserializeFromResponse<WooCommerceAttributeResponse>(responseBody, updateUrl, "PUT");
            return updated != null ? (updated.id, updated.slug) : (null, null);
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
                    var wooErr = TryDeserialize<WooErrorResponse>(err);

                    if (wooErr?.code == "term_exists")
                        continue; // OK

                    _logger.LogWarning("Failed to create attribute term ... {Error}", err);
                }
            }
        }

        /// <summary>
        /// Ensures a global attribute exists in the database and is synced to WooCommerce.
        /// Returns (WooCommerce attribute ID, taxonomy slug). Slug is the actual slug from WooCommerce (e.g. pa_xxx for Hebrew names).
        /// </summary>
        private async Task<(int? id, string? slug)> EnsureGlobalAttributeAsync(
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

            // Sync to WooCommerce; get actual (id, slug) from API
            var (wooCommerceId, wooSlug) = await SyncAttributeAsync(baseUrl, attribute, httpClient, cancelToken);
            
            if (wooCommerceId.HasValue && attribute.WooCommerceId != wooCommerceId.Value)
            {
                await _attributeStorage.UpdateAttributeWooCommerceIdAsync(attribute.Id, wooCommerceId.Value, cancelToken);
            }

            return (wooCommerceId, wooSlug);
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

            //TODO: Remove it
            productsToSync = productsToSync.Where(x => x.Id == 1255).ToList();

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

                // For variable products, ensure global attributes exist and use their IDs + actual slugs from WooCommerce
                var attributeMap = new Dictionary<string, int?>();   // attribute name -> WooCommerce ID
                var attributeSlugMap = new Dictionary<string, string>(); // attribute name -> WooCommerce taxonomy slug (e.g. pa_xxx)

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
                                // Ensure global attribute exists in DB and WooCommerce; get actual (id, slug) from API
                                var (wooAttrId, wooSlug) = await EnsureGlobalAttributeAsync(
                                    baseUrl,
                                    option.Name,
                                    attributeValues,
                                    siteId,
                                    httpClient,
                                    cancelToken);
                                
                                if (wooAttrId.HasValue)
                                {
                                    var key = NormalizeOptionKey(option.Name);
                                    attributeMap[key] = wooAttrId.Value;
                                    if (!string.IsNullOrEmpty(wooSlug))
                                        attributeSlugMap[key] = wooSlug;
                                }
                            }
                        }
                    }

                    // Build attributes array using global attribute IDs. Use actual WooCommerce taxonomy slug as name
                    // (from API) so WooCommerce links variations correctly; options remain display names for the dropdown.
                    var attributes = product.ProductOptions?
                        .Where(po => !po.IsDeleted && attributeMap.ContainsKey(NormalizeOptionKey(po.Name)))
                        .Select((option, index) => new
                        {
                            id = attributeMap[NormalizeOptionKey(option.Name)]!.Value,
                            // you can omit "name" entirely when using "id"
                            position = index,
                            visible = true,
                            variation = true,
                            options = option.ProductOptionValues?
                                .Select(pov => pov.Value)
                                .Where(v => !string.IsNullOrWhiteSpace(v))
                                .Select(v => v.Trim())
                                .Distinct()
                                .ToList() ?? new List<string>()
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
                    await SyncProductVariantsAsync(baseUrl, wooCommerceId.Value, product, attributeMap, attributeSlugMap, httpClient, cancelToken);
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
            Dictionary<string, string> attributeSlugMap,
            HttpClient httpClient,
            CancellationToken cancelToken)
        {
            var variants = product.ProductVariants?.Where(v => !v.IsDeleted).ToList() ?? new List<ProductVariant>();

            // When stock is not managed per variation ("במלאי" / "מלאי לפי כמות"), variations must not have their own stock column so they inherit from product.
            var stockManagedPerVariation = string.Equals(product.StockManagementType?.Name, "variation", StringComparison.OrdinalIgnoreCase);
            var productStockStatus = "instock";
            if (product.StockStatus?.Name == "out_of_stock" || product.Status?.Name == "outOfStock")
                productStockStatus = "outofstock";
            else if (product.StockStatus?.Name == "on_backorder")
                productStockStatus = "onbackorder";

            foreach (var variant in variants)
            {
                try
                {
                    var variantStockStatus = stockManagedPerVariation
                        ? ((variant.StockQuantity ?? 0) > 0 ? "instock" : "outofstock")
                        : productStockStatus;

                    var variantOptionValues = variant.ProductVariantOptionValues?
    .Where(x => !string.IsNullOrWhiteSpace(x.OptionName))
    .ToDictionary(
        x => NormalizeOptionKey(x.OptionName),
        x => (x.OptionValue ?? "").Trim()
    ) ?? new Dictionary<string, string>();


                    // Build variation attributes per WooCommerce API: id, name (taxonomy slug), slug, option (term value).
                    // Use actual WooCommerce slug from attributeSlugMap (kvp.Key = attribute name e.g. "צורת חיתוך").
                    // SlugifyAttributeName strips Hebrew and gives "pa_attr"; WooCommerce returns the real slug (e.g. pa_xxxx).
                    var variationAttributesList = new List<object>();
                    foreach (var kvp in variantOptionValues)
                    {
                        if (!attributeMap.TryGetValue(kvp.Key, out var attrIdNullable) || !attrIdNullable.HasValue)
                            continue;

                        var attrId = attrIdNullable.Value;

                        variationAttributesList.Add(new Dictionary<string, object>
                        {
                            ["id"] = attrId,
                            ["option"] = kvp.Value
                        });
                    }


                    if (variationAttributesList.Count == 0)
                    {
                        _logger.LogWarning("Variation {VariantId} for product {ProductId} has no matching attributes (attributeMap or ProductVariantOptionValues). Skipping this variation.", variant.Id, product.Id);
                        continue;
                    }

                    var wooVariation = new Dictionary<string, object>
                    {
                        ["regular_price"] = variant.Price?.ToString() ?? product.Price?.ToString() ?? "0",
                        ["sale_price"] = variant.SalePrice?.ToString() ?? "",
                        ["sku"] = variant.Sku ?? "",
                        ["manage_stock"] = stockManagedPerVariation,
                        ["stock_status"] = variantStockStatus,
                        ["weight"] = variant.Weight?.ToString() ?? "",
                        ["attributes"] = variationAttributesList
                    };
                    if (stockManagedPerVariation)
                    {
                        wooVariation["stock_quantity"] = variant.StockQuantity ?? 0;
                    }

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
        private static string NormalizeOptionKey(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim();
            // collapse multiple spaces (including Hebrew/RTL spacing issues)
            s = Regex.Replace(s, @"\s+", " ");
            return s;
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

