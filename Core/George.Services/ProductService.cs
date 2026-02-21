using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Data.Dto;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace George.Services
{
    public class ProductService : ServiceBase
    {
        private readonly ProductStorage _productStorage;
        private readonly CategoryStorage _categoryStorage;
        private readonly UserStorage _userStorage;
        private readonly MediaStorage _mediaStorage;
        private readonly WooCommerceService _wooCommerceService;
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ProductService(
            ILogger<ProductService> logger,
            IMapper mapper,
            CacheManager cache,
            ProductStorage productStorage,
            CategoryStorage categoryStorage,
            UserStorage userStorage,
            MediaStorage mediaStorage,
            WooCommerceService wooCommerceService,
            IServiceScopeFactory serviceScopeFactory
        ) : base(logger, mapper, cache)
        {
            _productStorage = productStorage;
            _categoryStorage = categoryStorage;
            _userStorage = userStorage;
            _mediaStorage = mediaStorage;
            _wooCommerceService = wooCommerceService;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task<IApiResponse<ApiListResponse<ProductRes>>> GetProductsAsync(
            ApiListReq<ProductFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<ProductRes>>
            {
                Data = new ApiListResponse<ProductRes>()
            };

            var res = await _productStorage.GetProductsAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(p => MapProductToRes(p));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<ProductRes>> GetProductAsync(int productId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductRes>();

            var product = await _productStorage.GetProductAsync(productId, cancelToken);
            if (product == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapProductToRes(product);
            return response;
        }

        public async Task<IApiResponse<ProductRes>> CreateProductAsync(CreateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductRes>();

            // Map request to DB model
            var product = MapReqToProduct(req);
            product.CreationUserId = AuthUser.Id;
            product.CreationTime = DateTime.UtcNow;
            product.IsActive = true;
            product.IsDeleted = false;

            // Handle lookups
            var lookupDto = MapToLookupDto(req);
            await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

            // Create product
            product = await _productStorage.CreateProductAsync(
                product, 
                req.SiteIds, 
                CombineCategoryIds(req.CategoryIds, req.SubcategoryIds),
                req.Tags,
                req.RelatedProductIds,
                req.ComplementaryProductIds,
                cancelToken);

            if (product != null)
            {
                // Create images (link to existing account media when URL matches; do not create new media)
                if (req.ImageUrls != null && req.ImageUrls.Any())
                {
                    var imageList = await ResolveImageUrlsToMediaAsync(product.AccountId, req.ImageUrls, createMediaIfMissing: true, cancelToken);
                    await _productStorage.CreateProductImagesAsync(product.Id, imageList, cancelToken);
                }

                // Create options and variants
                if (req.ProductOptions != null && req.ProductOptions.Any())
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.CreateProductOptionsAsync(product.Id, optionDtos, cancelToken);
                }

                if (req.Variants != null && req.Variants.Any())
                {
                    var variantDtos = req.Variants.Select(v => new ProductVariantDto
                    {
                        ImageUrl = v.ImageUrl,
                        OptionValues = v.OptionValues,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.StockQuantity,
                        Sku = v.Sku,
                        Weight = v.Weight
                    }).ToList();
                    var optionDtos = req.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.CreateProductVariantsAsync(product.Id, variantDtos, optionDtos, cancelToken);
                }

                // Reload with all relationships
                product = await _productStorage.GetProductAsync(product.Id, cancelToken);
                response.Data = MapProductToRes(product!);
                
                // Sync to WooCommerce only for the product's assigned sites (fire-and-forget to avoid blocking the response)
                if (product != null && req.SiteIds != null && req.SiteIds.Any())
                {
                    var productIdForSync = product.Id;
                    // Use the site IDs from the request (the sites the product is actually assigned to)
                    var assignedSiteIds = req.SiteIds.ToList();
                    
                    if (assignedSiteIds.Any())
                    {
                        _ = Task.Run(async () =>
                        {
                            await SyncProductToWooCommerceForAssignedSitesAsync(productIdForSync, assignedSiteIds, CancellationToken.None);
                        }, CancellationToken.None);
                    }
                }
            }

            return response;
        }

        public async Task<IApiResponse<ProductRes>> UpdateProductAsync(int productId, UpdateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductRes>();

            var existingProduct = await _productStorage.GetProductAsync(productId, cancelToken);
            if (existingProduct == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Merge request with existing product: if a property is null in req, keep the value from DB (partial update support for table quick-edit)
            var product = MergeReqWithExistingProduct(req, existingProduct);
            product.Id = productId;
            product.UpdateUserId = AuthUser.Id;

            // Handle lookups (only overwrites IDs when req has a value; existing IDs are already on product from merge)
            var lookupDto = MapToLookupDto(req);
            await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

            // For partial updates (e.g. table quick-edit), only pass category/tag/related/complementary when provided.
            // When null, storage keeps existing values; when empty list is passed, we would clear them.
            var categoryIdsToApply = (req.CategoryIds != null || req.SubcategoryIds != null)
                ? CombineCategoryIds(req.CategoryIds, req.SubcategoryIds)
                : null;

            product = await _productStorage.UpdateProductAsync(
                product,
                req.SiteIds,
                categoryIdsToApply,
                req.Tags,
                req.RelatedProductIds,
                req.ComplementaryProductIds,
                cancelToken);

            if (product != null)
            {
                // Update images (link to existing account media when URL matches; do not create new media)
                if (req.ImageUrls != null)
                {
                    var imageList = await ResolveImageUrlsToMediaAsync(existingProduct.AccountId, req.ImageUrls, createMediaIfMissing: true, cancelToken);
                    await _productStorage.CreateProductImagesAsync(productId, imageList, cancelToken);
                }

                // Update options and variants
                if (req.ProductOptions != null)
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.UpdateProductOptionsAsync(productId, optionDtos, cancelToken);
                }
                if (req.Variants != null)
                {
                    var variantDtos = req.Variants.Select(v => new ProductVariantDto
                    {
                        ImageUrl = v.ImageUrl,
                        OptionValues = v.OptionValues,
                        Price = v.Price,
                        SalePrice = v.SalePrice,
                        StockQuantity = v.StockQuantity,
                        Sku = v.Sku,
                        Weight = v.Weight
                    }).ToList();
                    var optionDtos = req.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _productStorage.UpdateProductVariantsAsync(productId, variantDtos, optionDtos, cancelToken);
                }

                // Reload with all relationships
                product = await _productStorage.GetProductAsync(productId, cancelToken);
                response.Data = MapProductToRes(product!);
                
                // Sync to WooCommerce only for the product's assigned sites (fire-and-forget to avoid blocking the response)
                if (product != null && req.SiteIds != null && req.SiteIds.Any())
                {
                    var productIdForSync = product.Id;
                    // Use the site IDs from the request (the sites the product is actually assigned to)
                    var assignedSiteIds = req.SiteIds.ToList();
                    
                    if (assignedSiteIds.Any())
                    {
                        _ = Task.Run(async () =>
                        {
                            await SyncProductToWooCommerceForAssignedSitesAsync(productIdForSync, assignedSiteIds, CancellationToken.None);
                        }, CancellationToken.None);
                    }
                }
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteProductAsync(int productId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _productStorage.DeleteProductAsync(productId, cancelToken);
            response.Data = result;

            return response;
        }

        public async Task<IApiResponse<BulkImportProductRes>> BulkImportProductsAsync(
            BulkImportProductReq req, 
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<BulkImportProductRes>
            {
                Data = new BulkImportProductRes()
            };

            if (req.Products == null || !req.Products.Any())
            {
                return CreateResponse(response, StatusCode.InvalidRequest, "No products provided");
            }

            var results = new List<BulkImportProductItemRes>();
            int created = 0;
            int updated = 0;
            int failed = 0;

            // Dictionary to cache category lookups/resolutions
            var categoryCache = new Dictionary<string, Category>();

            // Get user's AccountId once at the beginning (cache it)
            int? userAccountId = null;
            if (AuthUser != null && AuthUser.Id > 0)
            {
                var user = await _userStorage.GetUserAsync(AuthUser.Id, cancelToken);
                userAccountId = user?.AccountId;
            }

            // Process each product
            foreach (var productReqItem in req.Products)
            {
                CreateProductReq productReq = productReqItem;
                var itemResult = new BulkImportProductItemRes
                {
                    Name = productReq.Name,
                    Sku = productReq.Sku,
                    Success = false,
                    Action = "failed"
                };

                try
                {
                    Product? existingProduct = null;

                    // Use AccountId from request if provided, otherwise use user's AccountId
                    var accountIdForLookup = productReq.AccountId ?? userAccountId;

                    // Check if product exists: first by SKU, then by name+account when SKU is empty (e.g. products with variations)
                    if (req.UpdateIfExists)
                    {
                        if (!string.IsNullOrWhiteSpace(productReq.Sku))
                        {
                            existingProduct = await _productStorage.GetProductBySkuAsync(
                                productReq.Sku!,
                                accountIdForLookup,
                                cancelToken);
                        }
                        if (existingProduct == null && !string.IsNullOrWhiteSpace(productReq.Name))
                        {
                            var siteIdsForLookup = req.SiteIds ?? productReq.SiteIds;
                            existingProduct = await _productStorage.GetProductByNameAndAccountAsync(
                                productReq.Name!,
                                accountIdForLookup,
                                siteIdsForLookup,
                                cancelToken);
                        }
                    }

                    // Resolve categories - find or create them by path or ID
                    List<int>? resolvedCategoryIds = null;
                    List<int>? resolvedSubcategoryIds = null;

                    BulkImportProductItemReq? bulkProductReq = productReqItem as BulkImportProductItemReq;

                    // If category paths provided, resolve them first
                    if (bulkProductReq?.CategoryPaths != null && bulkProductReq.CategoryPaths.Any())
                    {
                        var allResolvedCategoryIds = new HashSet<int>(); // Use HashSet to avoid duplicates
                        
                        foreach (var categoryPath in bulkProductReq.CategoryPaths)
                        {
                            if (string.IsNullOrWhiteSpace(categoryPath)) continue;

                            // Find or create the leaf category (last in hierarchy)
                            var leafCategory = await _categoryStorage.FindOrCreateCategoryByPathAsync(
                                categoryPath,
                                accountIdForLookup,
                                req.SiteIds ?? productReq.SiteIds,
                                AuthUser?.Id,
                                cancelToken);

                            if (leafCategory != null)
                            {
                                // Add the leaf category and all its parents recursively
                                var categoryToAdd = leafCategory;
                                while (categoryToAdd != null)
                                {
                                    allResolvedCategoryIds.Add(categoryToAdd.Id);
                                    
                                    // Get parent if exists
                                    if (categoryToAdd.ParentCategoryId.HasValue)
                                    {
                                        categoryToAdd = await _categoryStorage.GetCategoryAsync(
                                            categoryToAdd.ParentCategoryId.Value, 
                                            cancelToken);
                                    }
                                    else
                                    {
                                        categoryToAdd = null;
                                    }
                                }
                            }
                        }

                        if (allResolvedCategoryIds.Any())
                        {
                            // Separate main categories from subcategories
                            resolvedCategoryIds = new List<int>();
                            resolvedSubcategoryIds = new List<int>();

                            foreach (var catId in allResolvedCategoryIds)
                            {
                                var cat = await _categoryStorage.GetCategoryAsync(catId, cancelToken);
                                if (cat?.ParentCategoryId == null)
                                {
                                    resolvedCategoryIds.Add(catId);
                                }
                                else
                                {
                                    resolvedSubcategoryIds.Add(catId);
                                }
                            }
                        }
                    }
                    // Otherwise use existing category IDs
                    else if (productReq.CategoryIds != null || productReq.SubcategoryIds != null)
                    {
                        var allCategoryIds = CombineCategoryIds(productReq.CategoryIds, productReq.SubcategoryIds);
                        var resolvedIds = await ResolveCategoryIdsAsync(
                            allCategoryIds, 
                            accountIdForLookup,
                            req.SiteIds ?? productReq.SiteIds,
                            categoryCache,
                            cancelToken);

                        // Separate main categories from subcategories
                        resolvedCategoryIds = new List<int>();
                        resolvedSubcategoryIds = new List<int>();

                        foreach (var catId in resolvedIds)
                        {
                            var cat = await _categoryStorage.GetCategoryAsync(catId, cancelToken);
                            if (cat?.ParentCategoryId == null)
                            {
                                resolvedCategoryIds.Add(catId);
                            }
                            else
                            {
                                resolvedSubcategoryIds.Add(catId);
                                // Also add parent as main category if not already included
                                if (cat.ParentCategoryId.HasValue && !resolvedCategoryIds.Contains(cat.ParentCategoryId.Value))
                                {
                                    resolvedCategoryIds.Add(cat.ParentCategoryId.Value);
                                }
                            }
                        }
                    }

                    if (existingProduct != null)
                    {
                        // Update existing product
                        var product = MapReqToProduct(productReq);
                        product.Id = existingProduct.Id;
                        product.UpdateUserId = AuthUser?.Id;
                        // Preserve AccountId from existing product or use from request/user
                        product.AccountId = existingProduct.AccountId ?? productReq.AccountId ?? userAccountId;

                        var lookupDto = MapToLookupDto(productReq);
                        await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

                        product = await _productStorage.UpdateProductAsync(
                            product,
                            productReq.SiteIds,
                            CombineCategoryIds(resolvedCategoryIds, resolvedSubcategoryIds),
                            productReq.Tags,
                            productReq.RelatedProductIds,
                            productReq.ComplementaryProductIds,
                            cancelToken);

                        if (product != null)
                        {
                            // Update images (link to existing account media; do not create new media)
                            if (productReq.ImageUrls != null)
                            {
                                var accountIdForImages = product.AccountId ?? userAccountId;
                                var imageList = await ResolveImageUrlsToMediaAsync(accountIdForImages, productReq.ImageUrls, createMediaIfMissing: true, cancelToken);
                                await _productStorage.CreateProductImagesAsync(product.Id, imageList, cancelToken);
                            }

                            if (productReq.ProductOptions != null)
                            {
                                var optionDtos = productReq.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.UpdateProductOptionsAsync(product.Id, optionDtos, cancelToken);
                            }

                            if (productReq.Variants != null)
                            {
                                var variantDtos = productReq.Variants.Select(v => new ProductVariantDto
                                {
                                    ImageUrl = v.ImageUrl,
                                    OptionValues = v.OptionValues,
                                    Price = v.Price,
                                    SalePrice = v.SalePrice,
                                    StockQuantity = v.StockQuantity,
                                    Sku = v.Sku,
                                    Weight = v.Weight
                                }).ToList();
                                var optionDtos = productReq.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.UpdateProductVariantsAsync(product.Id, variantDtos, optionDtos, cancelToken);
                            }

                            itemResult.Success = true;
                            itemResult.Action = "updated";
                            itemResult.ProductId = product.Id;
                            updated++;
                        }
                    }
                    else
                    {
                        // Create new product
                        var product = MapReqToProduct(productReq);
                        product.CreationUserId = AuthUser?.Id;
                        product.CreationTime = DateTime.UtcNow;
                        product.IsActive = true;
                        product.IsDeleted = false;
                        // Set AccountId from request or user if not provided
                        if (product.AccountId == null)
                        {
                            product.AccountId = productReq.AccountId ?? userAccountId;
                        }

                        var lookupDto = MapToLookupDto(productReq);
                        await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

                        product = await _productStorage.CreateProductAsync(
                            product,
                            productReq.SiteIds,
                            CombineCategoryIds(resolvedCategoryIds, resolvedSubcategoryIds),
                            productReq.Tags,
                            productReq.RelatedProductIds,
                            productReq.ComplementaryProductIds,
                            cancelToken);

                        if (product != null)
                        {
                            // Create images (link to existing account media; do not create new media)
                            if (productReq.ImageUrls != null && productReq.ImageUrls.Any())
                            {
                                var accountIdForImages = product.AccountId ?? userAccountId;
                                var imageList = await ResolveImageUrlsToMediaAsync(accountIdForImages, productReq.ImageUrls, createMediaIfMissing: true, cancelToken);
                                await _productStorage.CreateProductImagesAsync(product.Id, imageList, cancelToken);
                            }

                            if (productReq.ProductOptions != null && productReq.ProductOptions.Any())
                            {
                                var optionDtos = productReq.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.CreateProductOptionsAsync(product.Id, optionDtos, cancelToken);
                            }

                            if (productReq.Variants != null && productReq.Variants.Any())
                            {
                                var variantDtos = productReq.Variants.Select(v => new ProductVariantDto
                                {
                                    ImageUrl = v.ImageUrl,
                                    OptionValues = v.OptionValues,
                                    Price = v.Price,
                                    SalePrice = v.SalePrice,
                                    StockQuantity = v.StockQuantity,
                                    Sku = v.Sku,
                                    Weight = v.Weight
                                }).ToList();
                                var optionDtos = productReq.ProductOptions?.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _productStorage.CreateProductVariantsAsync(product.Id, variantDtos, optionDtos, cancelToken);
                            }

                            itemResult.Success = true;
                            itemResult.Action = "created";
                            itemResult.ProductId = product.Id;
                            created++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    itemResult.Success = false;
                    itemResult.Action = "failed";
                    itemResult.ErrorMessage = ex.Message;
                    failed++;
                    _logger.LogError(ex, $"Failed to import product '{productReq.Name}' (SKU: {productReq.Sku})");
                }

                results.Add(itemResult);
            }

            response.Data.Total = req.Products.Count;
            response.Data.Created = created;
            response.Data.Updated = updated;
            response.Data.Failed = failed;
            response.Data.Results = results;

            return response;
        }

        /// <summary>Resolve image URLs to (Url, MediaId). Uses existing account media when URL matches; when createMediaIfMissing is true (e.g. import), creates Media + AccountMedia for external URLs.</summary>
        private async Task<List<(string Url, int? MediaId)>> ResolveImageUrlsToMediaAsync(int? accountId, List<string>? imageUrls, bool createMediaIfMissing, CancellationToken cancelToken)
        {
            if (imageUrls == null || !imageUrls.Any()) return new List<(string, int?)>();
            if (!accountId.HasValue) return imageUrls.Select(u => (u, (int?)null)).ToList();
            var urlToMediaId = await _mediaStorage.GetMediaIdsByUrlsForAccountAsync(accountId.Value, imageUrls, cancelToken);
            var result = new List<(string Url, int? MediaId)>();
            foreach (var u in imageUrls)
            {
                var trimmed = u?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed)) continue;
                if (urlToMediaId.TryGetValue(trimmed, out var id))
                {
                    result.Add((u!, id));
                    continue;
                }
                if (createMediaIfMissing)
                {
                    var newId = await _mediaStorage.GetOrCreateMediaByUrlForAccountAsync(accountId.Value, trimmed, AuthUser?.Id, cancelToken);
                    result.Add((u!, newId));
                }
                else
                    result.Add((u!, (int?)null));
            }
            return result;
        }

        /// <summary>
        /// Resolve category IDs - verify they exist
        /// </summary>
        private async Task<List<int>> ResolveCategoryIdsAsync(
            List<int>? categoryIds,
            int? accountId,
            List<int>? siteIds,
            Dictionary<string, Category> categoryCache,
            CancellationToken cancelToken)
        {
            var resolvedIds = new List<int>();

            if (categoryIds == null || !categoryIds.Any()) return resolvedIds;

            // Verify categories exist
            foreach (var categoryId in categoryIds)
            {
                var category = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
                if (category != null && !category.IsDeleted)
                {
                    resolvedIds.Add(category.Id);
                }
            }

            return resolvedIds;
        }

        // Helper methods
        private Product MapReqToProduct(ProductReq req)
        {
            return new Product
            {
                Name = req.Name,
                ShortDescription = req.ShortDescription,
                LongDescription = req.LongDescription,
                Price = req.Price,
                SalePrice = req.SalePrice,
                SalePriceStartDate = req.SalePriceStartDate,
                SalePriceEndDate = req.SalePriceEndDate,
                CostPrice = req.CostPrice,
                Sku = req.Sku,
                StockQuantity = req.StockQuantity,
                Weight = req.Weight,
                IsKosher = req.IsKosher,
                IsWeighted = req.IsWeighted,
                AccountId = req.AccountId,
                SeoTitle = req.SeoTitle,
                SeoDescription = req.SeoDescription
            };
        }

        /// <summary>
        /// Merges update request with existing product for partial updates (e.g. quick-edit from table).
        /// If a property is null in the request, the existing value from the DB is kept.
        /// </summary>
        private static Product MergeReqWithExistingProduct(UpdateProductReq req, Product existing)
        {
            return new Product
            {
                Id = existing.Id,
                Name = !string.IsNullOrWhiteSpace(req.Name) ? req.Name : existing.Name,
                ShortDescription = req.ShortDescription ?? existing.ShortDescription,
                LongDescription = req.LongDescription ?? existing.LongDescription,
                Price = req.Price ?? existing.Price,
                SalePrice = req.SalePrice ?? existing.SalePrice,
                SalePriceStartDate = req.SalePriceStartDate ?? existing.SalePriceStartDate,
                SalePriceEndDate = req.SalePriceEndDate ?? existing.SalePriceEndDate,
                CostPrice = req.CostPrice ?? existing.CostPrice,
                Sku = req.Sku != null ? req.Sku : existing.Sku,
                StockQuantity = req.StockQuantity ?? existing.StockQuantity,
                Weight = req.Weight ?? existing.Weight,
                IsKosher = req.IsKosher ?? existing.IsKosher,
                IsWeighted = req.IsWeighted ?? existing.IsWeighted,
                AccountId = existing.AccountId ?? req.AccountId,
                SeoTitle = req.SeoTitle ?? existing.SeoTitle,
                SeoDescription = req.SeoDescription ?? existing.SeoDescription,
                // Preserve lookup IDs from existing; MapLookupsAsync will overwrite only when req has values
                BrandId = existing.BrandId,
                SupplierId = existing.SupplierId,
                StatusId = existing.StatusId,
                VisibilityId = existing.VisibilityId,
                StockManagementTypeId = existing.StockManagementTypeId,
                StockStatusId = existing.StockStatusId,
                ShippingClassId = existing.ShippingClassId,
                SetupTypeId = existing.SetupTypeId,
                WeightConfigId = existing.WeightConfigId
            };
        }


        private ProductRes MapProductToRes(Product product)
        {
            var res = new ProductRes
            {
                Id = product.Id,
                CreationTime = product.CreationTime,
                UpdatedDate = product.UpdatedDate,
                CreationUserId = product.CreationUserId,
                Name = product.Name,
                ShortDescription = product.ShortDescription,
                LongDescription = product.LongDescription,
                Price = product.Price,
                SalePrice = product.SalePrice,
                SalePriceStartDate = product.SalePriceStartDate,
                SalePriceEndDate = product.SalePriceEndDate,
                CostPrice = product.CostPrice,
                Sku = product.Sku,
                StockQuantity = product.StockQuantity,
                Weight = product.Weight,
                IsKosher = product.IsKosher,
                IsWeighted = product.IsWeighted,
                AccountId = product.AccountId,
                SeoTitle = product.SeoTitle,
                SeoDescription = product.SeoDescription
            };

            // Map images
            if (product.ProductImages != null && product.ProductImages.Any())
            {
                res.ImageUrls = product.ProductImages
                    .OrderBy(pi => pi.SortOrder)
                    .Select(pi => pi.Url)
                    .ToList();
            }

            // Map categories
            if (product.ProductCategories != null && product.ProductCategories.Any())
            {
                var mainCategories = product.ProductCategories
                    .Where(pc => pc.Category?.ParentCategoryId == null)
                    .Select(pc => pc.CategoryId)
                    .ToList();
                var subCategories = product.ProductCategories
                    .Where(pc => pc.Category?.ParentCategoryId != null)
                    .Select(pc => pc.CategoryId)
                    .ToList();
                
                res.CategoryIds = mainCategories;
                res.SubcategoryIds = subCategories;
            }

            // Map tags
            if (product.Tags != null && product.Tags.Any())
            {
                res.Tags = product.Tags.Select(t => t.Name).ToList();
            }

            // Map sites
            if (product.Sites != null && product.Sites.Any())
            {
                res.SiteIds = product.Sites.Select(s => s.Id).ToList();
            }

            // Map related products (נלווים)
            if (product.RelatedProducts != null && product.RelatedProducts.Any())
            {
                res.RelatedProductIds = product.RelatedProducts.Select(p => p.Id).ToList();
            }

            // Map complementary products (מוצרים משלימים)
            if (product.ComplementaryProducts != null && product.ComplementaryProducts.Any())
            {
                res.ComplementaryProductIds = product.ComplementaryProducts.Select(p => p.Id).ToList();
            }

            // Map lookups
            res.Status = product.Status?.Name;
            res.Visibility = product.Visibility?.Name;
            res.StockManagementType = product.StockManagementType?.Name;
            res.StockStatus = product.StockStatus?.Name;
            res.ShippingClass = product.ShippingClass?.Name;
            res.SetupType = product.SetupType?.Name;
            res.Brand = product.Brand?.Name;
            res.Supplier = product.Supplier?.Name;

            // Map options
            if (product.ProductOptions != null && product.ProductOptions.Any())
            {
                res.ProductOptions = product.ProductOptions
                    .Where(po => !po.IsDeleted)
                    .Select(po => new ProductOptionRes
                    {
                        Name = po.Name,
                        Values = po.ProductOptionValues?.Select(pov => pov.Value).ToList() ?? new List<string>()
                    })
                    .ToList();
            }

            // Map variants
            if (product.ProductVariants != null && product.ProductVariants.Any())
            {
                res.Variants = product.ProductVariants
                    .Where(pv => !pv.IsDeleted)
                    .Select(pv => new ProductVariantRes
                    {
                        ImageUrl = pv.ImageUrl,
                        Price = pv.Price,
                        SalePrice = pv.SalePrice,
                        StockQuantity = pv.StockQuantity,
                        Sku = pv.Sku,
                        Weight = pv.Weight,
                        OptionValues = pv.ProductVariantOptionValues?
                            .ToDictionary(pvov => pvov.OptionName ?? "", 
                                         pvov => pvov.OptionValue ?? "")
                    })
                    .ToList();
            }

            // Map weight config
            if (product.WeightConfig != null)
            {
                res.WeightConfig = new WeightConfigRes
                {
                    Unit = product.WeightConfig.Unit?.Name,
                    StartWeight = product.WeightConfig.StartWeight,
                    Step = product.WeightConfig.Step,
                    FixedWeightPerUnit = product.WeightConfig.FixedWeightPerUnit,
                    UnitWeight = product.WeightConfig.UnitWeight,
                    UnitWeightMode = product.WeightConfig.UnitWeightMode?.Name,
                    WeightOptions = product.WeightConfig.WeightOptions,
                    WeightByVariant = product.WeightConfig.WeightByVariant,
                    ShowPricePer100g = product.WeightConfig.ShowPricePer100g,
                    ShowUnitPrice = product.WeightConfig.ShowUnitPrice
                };
            }

            return res;
        }

        private List<int> CombineCategoryIds(List<int>? categoryIds, List<int>? subcategoryIds)
        {
            var combined = new List<int>();
            if (categoryIds != null) combined.AddRange(categoryIds);
            if (subcategoryIds != null) combined.AddRange(subcategoryIds);
            return combined;
        }

        private ProductLookupDto MapToLookupDto(ProductReq req)
        {
            return new ProductLookupDto
            {
                Status = req.Status,
                Visibility = req.Visibility,
                StockManagementType = req.StockManagementType,
                StockStatus = req.StockStatus,
                ShippingClass = req.ShippingClass,
                SetupType = req.SetupType,
                Brand = req.Brand,
                Supplier = req.Supplier,
                WeightConfig = req.WeightConfig != null ? new WeightConfigDto
                {
                    Unit = req.WeightConfig.Unit,
                    StartWeight = req.WeightConfig.StartWeight,
                    Step = req.WeightConfig.Step,
                    FixedWeightPerUnit = req.WeightConfig.FixedWeightPerUnit,
                    UnitWeight = req.WeightConfig.UnitWeight,
                    UnitWeightMode = req.WeightConfig.UnitWeightMode,
                    WeightOptions = req.WeightConfig.WeightOptions,
                    WeightByVariant = req.WeightConfig.WeightByVariant,
                    ShowPricePer100g = req.WeightConfig.ShowPricePer100g
                } : null
            };
        }

        private async Task SyncProductToWooCommerceForAssignedSitesAsync(int productId, List<int> assignedSiteIds, CancellationToken cancelToken)
        {
            if (assignedSiteIds == null || !assignedSiteIds.Any())
                return;

            // Create a scope for the background task to ensure services are available
            using var scope = _serviceScopeFactory.CreateScope();
            var wooCommerceService = scope.ServiceProvider.GetRequiredService<WooCommerceService>();
            var siteStorage = scope.ServiceProvider.GetRequiredService<SiteStorage>();

            // Sync product to WooCommerce only for assigned sites that have WooCommerce enabled
            foreach (var siteId in assignedSiteIds)
            {
                try
                {
                    // Check if this site has WooCommerce enabled
                    var site = await siteStorage.GetSiteAsync(siteId, cancelToken);
                    if (site == null || !site.WooCommerceEnabled.HasValue || !site.WooCommerceEnabled.Value)
                    {
                        _logger.LogDebug(
                            "Skipping WooCommerce sync for product {ProductId} to site {SiteId} - WooCommerce not enabled",
                            productId, siteId);
                        continue;
                    }

                    var syncReq = new WooCommerceSyncReq
                    {
                        SiteId = siteId,
                        ProductIds = new List<int> { productId }
                    };
                    var syncResponse = await wooCommerceService.SyncToWooCommerceAsync(syncReq, cancelToken);

                    if (syncResponse.Data?.Success == null || !syncResponse.Data.Success.Any())
                    {
                        _logger.LogWarning(
                            "Failed to sync product {ProductId} to WooCommerce for site {SiteId}: {Message}",
                            productId, siteId, syncResponse.Data?.Message ?? "Unknown error");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Successfully synced product {ProductId} to WooCommerce for site {SiteId}",
                            productId, siteId);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - we don't want WooCommerce sync failures to block product operations
                    _logger.LogError(ex,
                        "Error syncing product {ProductId} to WooCommerce for site {SiteId}",
                        productId, siteId);
                }
            }
        }

    }
}

