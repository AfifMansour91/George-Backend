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
    public class ProductService : ServiceBase
    {
        private readonly ProductStorage _productStorage;

        public ProductService(
            ILogger<ProductService> logger,
            IMapper mapper,
            CacheManager cache,
            ProductStorage productStorage
        ) : base(logger, mapper, cache)
        {
            _productStorage = productStorage;
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
                cancelToken);

            if (product != null)
            {
                // Create images
                if (req.ImageUrls != null && req.ImageUrls.Any())
                {
                    await _productStorage.CreateProductImagesAsync(product.Id, req.ImageUrls, cancelToken);
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
            }

            return response;
        }

        public async Task<IApiResponse<ProductRes>> UpdateProductAsync(int productId, UpdateProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<ProductRes>();

            var existingProduct = await _productStorage.GetProductAsync(productId, cancelToken);
            if (existingProduct == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Map request to DB model
            var product = MapReqToProduct(req);
            product.Id = productId;
            product.UpdateUserId = AuthUser.Id;

            // Handle lookups
            var lookupDto = MapToLookupDto(req);
            await _productStorage.MapLookupsAsync(product, lookupDto, cancelToken);

            // Update product
            product = await _productStorage.UpdateProductAsync(
                product,
                req.SiteIds,
                CombineCategoryIds(req.CategoryIds, req.SubcategoryIds),
                req.Tags,
                cancelToken);

            if (product != null)
            {
                // Update images
                if (req.ImageUrls != null)
                {
                    await _productStorage.CreateProductImagesAsync(productId, req.ImageUrls, cancelToken);
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
                    ShowPricePer100g = product.WeightConfig.ShowPricePer100g
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

    }
}

