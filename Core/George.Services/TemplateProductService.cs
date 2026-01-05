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

        public TemplateProductService(
            ILogger<TemplateProductService> logger,
            IMapper mapper,
            CacheManager cache,
            TemplateProductStorage templateProductStorage
        ) : base(logger, mapper, cache)
        {
            _templateProductStorage = templateProductStorage;
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

            // Map request to DB model
            var templateProduct = MapReqToTemplateProduct(req);
            templateProduct.Id = templateProductId;
            templateProduct.UpdateUserId = AuthUser.Id;

            // Handle lookups
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
                    ShowPricePer100g = templateProduct.WeightConfig.ShowPricePer100g
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

