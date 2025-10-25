using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class CatalogService : ServiceBase
    {
        private readonly CatalogStorage _catalogStorage;

        public CatalogService(
            ILogger<CatalogService> logger,
            IMapper mapper,
            CacheManager cache,
            CatalogStorage catalogStorage
        ) : base(logger, mapper, cache)
        {
            _catalogStorage = catalogStorage;
        }

        // List ProductTemplate
        public async Task<IApiResponse<ApiListResponse<CatalogListRow>>> GetCatalogProductsAsync(
            ApiListReq<CatalogProductListFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<CatalogListRow>>()
            {
                Data = new ApiListResponse<CatalogListRow>()
            };

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var res = await _catalogStorage.GetProductTemplatesAsync(request.Filter, request, cancelToken);

            response.Data.Items = res.Items ?? new List<CatalogListRow>();
            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        // Detail ProductTemplate
        public async Task<IApiResponse<CatalogProductDetailRes>> GetCatalogProductDetailAsync(long templateId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CatalogProductDetailRes>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var model = await _catalogStorage.GetProductTemplateDetailAsync(templateId, cancelToken);
            if (model == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // map manually for MVP
            response.Data = new CatalogProductDetailRes
            {
                ProductTemplateId = model.Id,
                Sku = model.Sku,
                Title = model.Title,
                ShortDescription = model.ShortDescription,
                DescriptionHtml = model.DescriptionHtml,
                ProductTypeId = model.ProductTypeId,
                BrandId = model.BrandId,
                SupplierId = model.SupplierId,
                IsKosherDefault = model.IsKosherDefault,
                KosherStatusId = model.KosherStatusId,
                WeightPricingModelId = model.WeightPricingModelId,
                ShowPricePer100g = model.ShowPricePer100g,
                BaseUnitPrice = model.BaseUnitPrice,
                BaseUnitId = model.BaseUnitId,
                BaseWeightGrams = model.BaseWeightGrams,
                IsActive = model.IsActive,
                Media = model.ProductTemplateMedia?
                    .OrderBy(m => m.SortOrder)
                    .Select(m => new CatalogProductMediaRes
                    {
                        Id = m.Id,
                        Url = m.Url,
                        AltText = m.AltText,
                        SortOrder = m.SortOrder,
                        IsPrimary = m.IsPrimary
                    }).ToList() ?? new(),
                Attributes = model.ProductTemplateAttributes?
                    .Select(a => new CatalogProductAttributeRes
                    {
                        ProductTemplateAttributeId = a.Id,
                        AttributeId = a.AttributeId,
                        AttributeName = "", // you can join Attribute.Name with Include if you add nav
                        IsVariantAxis = a.IsVariantAxis,
                        Options = a.ProductTemplateAttributeOptions?
                            .Select(o => new CatalogProductAttributeOptionRes
                            {
                                ProductTemplateAttributeOptionId = o.Id,
                                AttributeOptionId = o.AttributeOptionId,
                                Value = "", // also needs join to AttributeOption table to get Value
                                SortOrder = 0 // you'd need that join too
                            }).ToList() ?? new()
                    }).ToList() ?? new(),
                SelectableWeights = model.ProductTemplateSelectableWeights?
                    .OrderBy(w => w.SortOrder)
                    .Select(w => new CatalogSelectableWeightRes
                    {
                        Id = w.Id,
                        Label = w.Label,
                        WeightGrams = w.WeightGrams,
                        SortOrder = w.SortOrder
                    }).ToList() ?? new(),
                CategoryIds = model.ProductTemplateCategories?
                    .Select(pc => pc.CategoryId)
                    .ToList() ?? new(),
                BusinessTypeIds = model.ProductTemplateBusinessTypes?
                    .Select(bt => bt.BusinessTypeId)
                    .ToList() ?? new()
            };

            return response;
        }

        // Create new ProductTemplate
        public async Task<IApiResponse<CatalogProductDetailRes>> CreateCatalogProductAsync(CreateCatalogProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CatalogProductDetailRes>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var entity = new ProductTemplate
            {
                Sku = req.Sku,
                Title = req.Title,
                ShortDescription = req.ShortDescription,
                DescriptionHtml = req.DescriptionHtml,
                ProductTypeId = req.ProductTypeId,
                BrandId = req.BrandId,
                SupplierId = req.SupplierId,
                IsKosherDefault = req.IsKosherDefault,
                KosherStatusId = req.KosherStatusId,
                WeightPricingModelId = req.WeightPricingModelId,
                ShowPricePer100g = req.ShowPricePer100g,
                BaseUnitPrice = req.BaseUnitPrice,
                BaseUnitId = req.BaseUnitId,
                BaseWeightGrams = req.BaseWeightGrams,
                IsActive = req.IsActive,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = null
            };

            // TODO: also create medias, attributes, selectable weights, categories, businessTypes here
            // For MVP we insert bare minimum then extend.

            entity = await _catalogStorage.CreateProductTemplateAsync(entity, cancelToken);

            return await GetCatalogProductDetailAsync(entity.Id, cancelToken);
        }

        // Update ProductTemplate
        public async Task<IApiResponse<CatalogProductDetailRes>> UpdateCatalogProductAsync(long templateId, UpdateCatalogProductReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CatalogProductDetailRes>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            // build patch model
            var patch = new ProductTemplatePatchModel
            {
                SkuSet = req.Sku != null,
                Sku = req.Sku,

                TitleSet = req.Title != null,
                Title = req.Title,

                ShortDescriptionSet = req.ShortDescription != null,
                ShortDescription = req.ShortDescription,

                DescriptionHtmlSet = req.DescriptionHtml != null,
                DescriptionHtml = req.DescriptionHtml,

                ProductTypeIdSet = true,
                ProductTypeId = req.ProductTypeId,

                BrandIdSet = true,
                BrandId = req.BrandId,

                SupplierIdSet = true,
                SupplierId = req.SupplierId,

                IsKosherDefaultSet = true,
                IsKosherDefault = req.IsKosherDefault,

                KosherStatusIdSet = true,
                KosherStatusId = req.KosherStatusId,

                WeightPricingModelIdSet = true,
                WeightPricingModelId = req.WeightPricingModelId,

                ShowPricePer100gSet = true,
                ShowPricePer100g = req.ShowPricePer100g,

                BaseUnitPriceSet = true,
                BaseUnitPrice = req.BaseUnitPrice,

                BaseUnitIdSet = true,
                BaseUnitId = req.BaseUnitId,

                BaseWeightGramsSet = true,
                BaseWeightGrams = req.BaseWeightGrams,

                IsActiveSet = true,
                IsActive = req.IsActive
            };

            var updated = await _catalogStorage.UpdateProductTemplateAsync(templateId, patch, AuthUser.Id, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            return await GetCatalogProductDetailAsync(templateId, cancelToken);
        }

        // Toggle active/inactive
        public async Task<IApiResponse<bool>> SetCatalogProductStatusAsync(long templateId, BoolReq request, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var updated = await _catalogStorage.SetProductTemplateStatusAsync(templateId, request.Value, AuthUser.Id, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = updated.IsActive;
            return response;
        }

        // Lookups for dropdowns (brands, suppliers, kosher, weight models, etc.)
        public async Task<IApiResponse<CatalogLookupsRes>> GetLookupsAsync(CancellationToken cancelToken)
        {
            var response = new ApiResponse<CatalogLookupsRes>();

            // SuperAdmin only for now. Later, account admins may also need this to edit AccountProduct.
            // You could relax this check later.
            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var (brands, suppliers, kosherStatuses, weightPricingModels, units, businessTypes) = await _catalogStorage.GetLookupsAsync(cancelToken);

            response.Data = new CatalogLookupsRes
            {
                Brands = brands,
                Suppliers = suppliers,
                KosherStatuses = kosherStatuses,
                WeightPricingModels = weightPricingModels,
                Units = units,
                BusinessTypes = businessTypes
            };
            return response;
        }

        // Global categories admin
        public async Task<IApiResponse<List<GlobalCategoryNodeRes>>> GetGlobalCategoriesAsync(CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<GlobalCategoryNodeRes>>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var categories = await _catalogStorage.GetGlobalCategoriesAsync(cancelToken);

            response.Data = categories.Select(c => new GlobalCategoryNodeRes
            {
                CategoryId = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                SortOrder = c.SortOrder,
                IsActive = c.IsActive
            }).ToList();

            return response;
        }

        public async Task<IApiResponse<GlobalCategoryNodeRes>> UpdateGlobalCategoryAsync(int categoryId, UpdateGlobalCategoryReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalCategoryNodeRes>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var updated = await _catalogStorage.UpdateGlobalCategoryAsync(categoryId, req, cancelToken);
            if (updated == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = new GlobalCategoryNodeRes
            {
                CategoryId = updated.Id,
                Name = updated.Name,
                Slug = updated.Slug,
                SortOrder = updated.SortOrder,
                IsActive = updated.IsActive
            };

            return response;
        }

        public async Task<IApiResponse<GlobalCategoryNodeRes>> CreateGlobalCategoryAsync(CreateGlobalCategoryReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalCategoryNodeRes>();

            if (!AuthUser.IsMaster)
                return CreateResponse(response, StatusCode.UnauthorizedData);

            var created = await _catalogStorage.CreateGlobalCategoryAsync(req, cancelToken);

            response.Data = new GlobalCategoryNodeRes
            {
                CategoryId = created.Id,
                Name = created.Name,
                Slug = created.Slug,
                SortOrder = created.SortOrder,
                IsActive = created.IsActive
            };

            return response;
        }
    }
}
