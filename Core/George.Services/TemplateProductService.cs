using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Data.Dto;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class TemplateProductService : ServiceBase
    {
        private readonly TemplateProductStorage _templateProductStorage;
        private readonly GlobalCategoryStorage _globalCategoryStorage;

        public TemplateProductService(
            ILogger<TemplateProductService> logger,
            IMapper mapper,
            CacheManager cache,
            TemplateProductStorage templateProductStorage,
            GlobalCategoryStorage globalCategoryStorage
        ) : base(logger, mapper, cache)
        {
            _templateProductStorage = templateProductStorage;
            _globalCategoryStorage = globalCategoryStorage;
        }

        public async Task<IApiResponse<ApiListResponse<TemplateProductRes>>> GetTemplateProductsAsync(
            ApiListReq<TemplateProductFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<TemplateProductRes>>
            {
                Data = new ApiListResponse<TemplateProductRes>()
            };

            var res = await _templateProductStorage.GetTemplateProductsAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(tp => MapTemplateProductToRes(tp));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<TemplateProductRes>> GetTemplateProductAsync(int templateProductId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<TemplateProductRes>();

            var templateProduct = await _templateProductStorage.GetTemplateProductAsync(templateProductId, cancelToken);
            if (templateProduct == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapTemplateProductToRes(templateProduct);
            return response;
        }

        public async Task<IApiResponse<TemplateProductRes>> CreateTemplateProductAsync(CreateTemplateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<TemplateProductRes>();

            // Map request to DB model
            var templateProduct = MapReqToTemplateProduct(req);
            templateProduct.CreationUserId = AuthUser.Id;
            templateProduct.CreationTime = DateTime.UtcNow;
            templateProduct.IsDeleted = false;

            // Handle lookups
            var lookupDto = MapToLookupDto(req);
            await _templateProductStorage.MapLookupsAsync(templateProduct, lookupDto, cancelToken);

            // Combine category IDs
            var categoryIds = CombineCategoryIds(req.CategoryIds, req.SubcategoryIds);

            // Create template product
            templateProduct = await _templateProductStorage.CreateTemplateProductAsync(
                templateProduct, 
                req.SiteIds, 
                categoryIds,
                req.Tags,
                cancelToken);

            if (templateProduct != null)
            {
                // Create images
                if (req.ImageUrls != null && req.ImageUrls.Any())
                {
                    await _templateProductStorage.CreateTemplateProductImagesAsync(templateProduct.Id, req.ImageUrls, cancelToken);
                }

                // Create options and variants
                if (req.ProductOptions != null && req.ProductOptions.Any())
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _templateProductStorage.CreateTemplateProductOptionsAsync(templateProduct.Id, optionDtos, cancelToken);
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
                    await _templateProductStorage.CreateTemplateProductVariantsAsync(templateProduct.Id, variantDtos, optionDtos, cancelToken);
                }

                // Reload with all relationships
                templateProduct = await _templateProductStorage.GetTemplateProductAsync(templateProduct.Id, cancelToken);
                response.Data = MapTemplateProductToRes(templateProduct!);
            }

            return response;
        }

        public async Task<IApiResponse<TemplateProductRes>> UpdateTemplateProductAsync(int templateProductId, UpdateTemplateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<TemplateProductRes>();

            var existingTemplateProduct = await _templateProductStorage.GetTemplateProductAsync(templateProductId, cancelToken);
            if (existingTemplateProduct == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Merge request with existing: if a property is null in req, keep the value from DB (partial update support for table quick-edit)
            var templateProduct = MergeReqWithExistingTemplateProduct(req, existingTemplateProduct);
            templateProduct.Id = templateProductId;
            templateProduct.UpdateUserId = AuthUser.Id;

            // Handle lookups (only overwrites IDs when req has a value; existing IDs are already on product from merge)
            var lookupDto = MapToLookupDto(req);
            await _templateProductStorage.MapLookupsAsync(templateProduct, lookupDto, cancelToken);

            // Combine category IDs
            var categoryIds = CombineCategoryIds(req.CategoryIds, req.SubcategoryIds);

            // Update template product
            templateProduct = await _templateProductStorage.UpdateTemplateProductAsync(
                templateProduct,
                req.SiteIds,
                categoryIds,
                req.Tags,
                cancelToken);

            if (templateProduct != null)
            {
                // Update images
                if (req.ImageUrls != null)
                {
                    await _templateProductStorage.CreateTemplateProductImagesAsync(templateProductId, req.ImageUrls, cancelToken);
                }

                // Update options and variants
                if (req.ProductOptions != null)
                {
                    var optionDtos = req.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                    await _templateProductStorage.UpdateTemplateProductOptionsAsync(templateProductId, optionDtos, cancelToken);
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
                    await _templateProductStorage.UpdateTemplateProductVariantsAsync(templateProductId, variantDtos, optionDtos, cancelToken);
                }

                // Reload with all relationships
                templateProduct = await _templateProductStorage.GetTemplateProductAsync(templateProductId, cancelToken);
                response.Data = MapTemplateProductToRes(templateProduct!);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteTemplateProductAsync(int templateProductId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _templateProductStorage.DeleteTemplateProductAsync(templateProductId, cancelToken);
            response.Data = result;

            return response;
        }

        public async Task<IApiResponse<BulkImportTemplateProductRes>> BulkImportTemplateProductsAsync(
            BulkImportTemplateProductReq req,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<BulkImportTemplateProductRes>
            {
                Data = new BulkImportTemplateProductRes()
            };

            if (req.Products == null || !req.Products.Any())
            {
                return CreateResponse(response, StatusCode.InvalidRequest, "No products provided");
            }

            var results = new List<BulkImportTemplateProductItemRes>();
            int created = 0;
            int updated = 0;
            int failed = 0;

            // Dictionary to cache global category lookups/resolutions
            var categoryCache = new Dictionary<string, GlobalCategory>();

            // Process each product
            foreach (var productReqItem in req.Products)
            {
                CreateTemplateProductReq productReq = productReqItem;
                var itemResult = new BulkImportTemplateProductItemRes
                {
                    Name = productReq.Name,
                    Sku = productReq.Sku,
                    Success = false,
                    Action = "failed"
                };

                try
                {
                    TemplateProduct? existingProduct = null;

                    // Check if product exists by SKU
                    if (req.UpdateIfExists && !string.IsNullOrWhiteSpace(productReq.Sku))
                    {
                        existingProduct = await _templateProductStorage.GetTemplateProductBySkuAsync(
                            productReq.Sku,
                            cancelToken);
                    }

                    // Resolve global categories - find or create them by path or ID
                    List<int>? resolvedCategoryIds = null;
                    List<int>? resolvedSubcategoryIds = null;

                    BulkImportTemplateProductItemReq? bulkProductReq = productReqItem as BulkImportTemplateProductItemReq;

                    // If category paths provided, resolve them first
                    if (bulkProductReq?.CategoryPaths != null && bulkProductReq.CategoryPaths.Any())
                    {
                        var allResolvedCategoryIds = new HashSet<int>(); // Use HashSet to avoid duplicates
                        
                        foreach (var categoryPath in bulkProductReq.CategoryPaths)
                        {
                            if (string.IsNullOrWhiteSpace(categoryPath)) continue;

                            // Find or create the leaf category (last in hierarchy)
                            var leafCategory = await _globalCategoryStorage.FindOrCreateGlobalCategoryByPathAsync(
                                categoryPath,
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
                                    if (categoryToAdd.ParentGlobalCategoryId.HasValue)
                                    {
                                        categoryToAdd = await _globalCategoryStorage.GetGlobalCategoryAsync(
                                            categoryToAdd.ParentGlobalCategoryId.Value, 
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
                                var cat = await _globalCategoryStorage.GetGlobalCategoryAsync(catId, cancelToken);
                                if (cat?.ParentGlobalCategoryId == null)
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
                        var resolvedIds = await ResolveGlobalCategoryIdsAsync(allCategoryIds, cancelToken);

                        // Separate main categories from subcategories
                        resolvedCategoryIds = new List<int>();
                        resolvedSubcategoryIds = new List<int>();

                        foreach (var catId in resolvedIds)
                        {
                            var cat = await _globalCategoryStorage.GetGlobalCategoryAsync(catId, cancelToken);
                            if (cat?.ParentGlobalCategoryId == null)
                            {
                                resolvedCategoryIds.Add(catId);
                            }
                            else
                            {
                                resolvedSubcategoryIds.Add(catId);
                                // Also add parent as main category if not already included
                                if (cat.ParentGlobalCategoryId.HasValue && !resolvedCategoryIds.Contains(cat.ParentGlobalCategoryId.Value))
                                {
                                    resolvedCategoryIds.Add(cat.ParentGlobalCategoryId.Value);
                                }
                            }
                        }
                    }

                    if (existingProduct != null)
                    {
                        // Update existing product
                        var templateProduct = MapReqToTemplateProduct(productReq);
                        templateProduct.Id = existingProduct.Id;
                        templateProduct.UpdateUserId = AuthUser?.Id;

                        var lookupDto = MapToLookupDto(productReq);
                        await _templateProductStorage.MapLookupsAsync(templateProduct, lookupDto, cancelToken);

                        templateProduct = await _templateProductStorage.UpdateTemplateProductAsync(
                            templateProduct,
                            req.SiteIds ?? productReq.SiteIds,
                            CombineCategoryIds(resolvedCategoryIds, resolvedSubcategoryIds),
                            productReq.Tags,
                            cancelToken);

                        if (templateProduct != null)
                        {
                            // Update images, options, variants
                            if (productReq.ImageUrls != null)
                            {
                                await _templateProductStorage.CreateTemplateProductImagesAsync(templateProduct.Id, productReq.ImageUrls, cancelToken);
                            }

                            if (productReq.ProductOptions != null)
                            {
                                var optionDtos = productReq.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _templateProductStorage.UpdateTemplateProductOptionsAsync(templateProduct.Id, optionDtos, cancelToken);
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
                                await _templateProductStorage.UpdateTemplateProductVariantsAsync(templateProduct.Id, variantDtos, optionDtos, cancelToken);
                            }

                            itemResult.Success = true;
                            itemResult.Action = "updated";
                            itemResult.ProductId = templateProduct.Id;
                            updated++;
                        }
                    }
                    else
                    {
                        // Create new product
                        var templateProduct = MapReqToTemplateProduct(productReq);
                        templateProduct.CreationUserId = AuthUser?.Id;
                        templateProduct.CreationTime = DateTime.UtcNow;
                        templateProduct.IsDeleted = false;

                        var lookupDto = MapToLookupDto(productReq);
                        await _templateProductStorage.MapLookupsAsync(templateProduct, lookupDto, cancelToken);

                        templateProduct = await _templateProductStorage.CreateTemplateProductAsync(
                            templateProduct,
                            req.SiteIds ?? productReq.SiteIds,
                            CombineCategoryIds(resolvedCategoryIds, resolvedSubcategoryIds),
                            productReq.Tags,
                            cancelToken);

                        if (templateProduct != null)
                        {
                            // Create images, options, variants
                            if (productReq.ImageUrls != null && productReq.ImageUrls.Any())
                            {
                                await _templateProductStorage.CreateTemplateProductImagesAsync(templateProduct.Id, productReq.ImageUrls, cancelToken);
                            }

                            if (productReq.ProductOptions != null && productReq.ProductOptions.Any())
                            {
                                var optionDtos = productReq.ProductOptions.Select(o => new ProductOptionDto { Name = o.Name, Values = o.Values ?? new List<string>() }).ToList();
                                await _templateProductStorage.CreateTemplateProductOptionsAsync(templateProduct.Id, optionDtos, cancelToken);
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
                                await _templateProductStorage.CreateTemplateProductVariantsAsync(templateProduct.Id, variantDtos, optionDtos, cancelToken);
                            }

                            itemResult.Success = true;
                            itemResult.Action = "created";
                            itemResult.ProductId = templateProduct.Id;
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
                    _logger.LogError(ex, $"Failed to import template product '{productReq.Name}' (SKU: {productReq.Sku})");
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

        /// <summary>
        /// Resolve global category IDs - verify they exist
        /// </summary>
        private async Task<List<int>> ResolveGlobalCategoryIdsAsync(
            List<int>? categoryIds,
            CancellationToken cancelToken)
        {
            var resolvedIds = new List<int>();

            if (categoryIds == null || !categoryIds.Any()) return resolvedIds;

            // Verify categories exist
            foreach (var categoryId in categoryIds)
            {
                var category = await _globalCategoryStorage.GetGlobalCategoryAsync(categoryId, cancelToken);
                if (category != null && !category.IsDeleted)
                {
                    resolvedIds.Add(category.Id);
                }
            }

            return resolvedIds;
        }

        // Helper methods
        private TemplateProduct MapReqToTemplateProduct(TemplateProductReq req)
        {
            return new TemplateProduct
            {
                TemplateId = req.TemplateId,
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
                SeoTitle = req.SeoTitle,
                SeoDescription = req.SeoDescription,
                SourceProductId = req.SourceProductId
            };
        }

        /// <summary>
        /// Merges update request with existing template product for partial updates (e.g. quick-edit from table).
        /// If a property is null in the request, the existing value from the DB is kept.
        /// </summary>
        private static TemplateProduct MergeReqWithExistingTemplateProduct(UpdateTemplateProductReq req, TemplateProduct existing)
        {
            return new TemplateProduct
            {
                Id = existing.Id,
                TemplateId = req.TemplateId ?? existing.TemplateId,
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
                SeoTitle = req.SeoTitle ?? existing.SeoTitle,
                SeoDescription = req.SeoDescription ?? existing.SeoDescription,
                SourceProductId = req.SourceProductId ?? existing.SourceProductId,
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

        private TemplateProductRes MapTemplateProductToRes(TemplateProduct templateProduct)
        {
            var res = new TemplateProductRes
            {
                Id = templateProduct.Id,
                CreationTime = templateProduct.CreationTime,
                UpdatedDate = templateProduct.UpdatedDate,
                CreationUserId = templateProduct.CreationUserId,
                TemplateId = templateProduct.TemplateId,
                Name = templateProduct.Name,
                ShortDescription = templateProduct.ShortDescription,
                LongDescription = templateProduct.LongDescription,
                Price = templateProduct.Price,
                SalePrice = templateProduct.SalePrice,
                SalePriceStartDate = templateProduct.SalePriceStartDate,
                SalePriceEndDate = templateProduct.SalePriceEndDate,
                CostPrice = templateProduct.CostPrice,
                Sku = templateProduct.Sku,
                StockQuantity = templateProduct.StockQuantity,
                Weight = templateProduct.Weight,
                IsKosher = templateProduct.IsKosher,
                IsWeighted = templateProduct.IsWeighted,
                SeoTitle = templateProduct.SeoTitle,
                SeoDescription = templateProduct.SeoDescription,
                SourceProductId = templateProduct.SourceProductId
            };

            // Map images
            if (templateProduct.TemplateProductImages != null && templateProduct.TemplateProductImages.Any())
            {
                res.ImageUrls = templateProduct.TemplateProductImages
                    .OrderBy(tpi => tpi.SortOrder)
                    .Select(tpi => tpi.Url)
                    .ToList();
            }

            // Map categories - get GlobalCategory IDs directly from TemplateProductCategory
            if (templateProduct.TemplateProductCategories != null && templateProduct.TemplateProductCategories.Any())
            {
                var mainCategories = templateProduct.TemplateProductCategories
                    .Where(tpc => tpc.GlobalCategory != null && 
                                 tpc.GlobalCategory.ParentGlobalCategoryId == null)
                    .Select(tpc => tpc.GlobalCategoryId)
                    .Distinct()
                    .ToList();
                var subCategories = templateProduct.TemplateProductCategories
                    .Where(tpc => tpc.GlobalCategory != null && 
                                 tpc.GlobalCategory.ParentGlobalCategoryId != null)
                    .Select(tpc => tpc.GlobalCategoryId)
                    .Distinct()
                    .ToList();
                
                res.CategoryIds = mainCategories;
                res.SubcategoryIds = subCategories;
            }

            // Map tags
            if (templateProduct.Tags != null && templateProduct.Tags.Any())
            {
                res.Tags = templateProduct.Tags.Select(t => t.Name).ToList();
            }

            // Map sites
            if (templateProduct.Sites != null && templateProduct.Sites.Any())
            {
                res.SiteIds = templateProduct.Sites.Select(s => s.Id).ToList();
            }

            // Map lookups
            res.Status = templateProduct.Status?.Name;
            res.Visibility = templateProduct.Visibility?.Name;
            res.StockManagementType = templateProduct.StockManagementType?.Name;
            res.StockStatus = templateProduct.StockStatus?.Name;
            res.ShippingClass = templateProduct.ShippingClass?.Name;
            res.SetupType = templateProduct.SetupType?.Name;
            res.Brand = templateProduct.Brand?.Name;
            res.Supplier = templateProduct.Supplier?.Name;

            // Map options
            if (templateProduct.TemplateProductOptions != null && templateProduct.TemplateProductOptions.Any())
            {
                res.ProductOptions = templateProduct.TemplateProductOptions
                    .Where(tpo => !tpo.IsDeleted)
                    .Select(tpo => new TemplateProductOptionRes
                    {
                        Name = tpo.Name,
                        Values = tpo.TemplateProductOptionValues?.Select(tpov => tpov.Value).ToList() ?? new List<string>()
                    })
                    .ToList();
            }

            // Map variants
            if (templateProduct.TemplateProductVariants != null && templateProduct.TemplateProductVariants.Any())
            {
                res.Variants = templateProduct.TemplateProductVariants
                    .Where(tpv => !tpv.IsDeleted)
                    .Select(tpv => new TemplateProductVariantRes
                    {
                        ImageUrl = tpv.ImageUrl,
                        Price = tpv.Price,
                        SalePrice = tpv.SalePrice,
                        StockQuantity = tpv.StockQuantity,
                        Sku = tpv.Sku,
                        Weight = tpv.Weight,
                        OptionValues = tpv.TemplateProductVariantOptionValues?
                            .ToDictionary(tpvov => tpvov.OptionName ?? "", 
                                         tpvov => tpvov.OptionValue ?? "")
                    })
                    .ToList();
            }

            // Map weight config
            if (templateProduct.WeightConfig != null)
            {
                res.WeightConfig = new George.Services.Response.WeightConfigRes
                {
                    Unit = templateProduct.WeightConfig.Unit?.Name,
                    StartWeight = templateProduct.WeightConfig.StartWeight,
                    Step = templateProduct.WeightConfig.Step,
                    FixedWeightPerUnit = templateProduct.WeightConfig.FixedWeightPerUnit,
                    UnitWeight = templateProduct.WeightConfig.UnitWeight,
                    UnitWeightMode = templateProduct.WeightConfig.UnitWeightMode?.Name,
                    WeightOptions = templateProduct.WeightConfig.WeightOptions,
                    WeightByVariant = templateProduct.WeightConfig.WeightByVariant,
                    ShowPricePer100g = templateProduct.WeightConfig.ShowPricePer100g,
                    ShowUnitPrice = templateProduct.WeightConfig.ShowUnitPrice
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

        private ProductLookupDto MapToLookupDto(TemplateProductReq req)
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
    }
}

