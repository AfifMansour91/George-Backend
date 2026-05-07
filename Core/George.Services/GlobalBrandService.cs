using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    /// <summary>
    /// GlobalBrand business logic. Mirrors <see cref="GlobalCategoryService"/>.
    /// Reuses the slug normalizer in <see cref="BrandService"/> so site-level and global-level
    /// brands produce consistent slugs.
    /// </summary>
    public class GlobalBrandService : ServiceBase
    {
        private readonly GlobalBrandStorage _globalBrandStorage;

        public GlobalBrandService(
            ILogger<GlobalBrandService> logger,
            IMapper mapper,
            CacheManager cache,
            GlobalBrandStorage globalBrandStorage)
            : base(logger, mapper, cache)
        {
            _globalBrandStorage = globalBrandStorage;
        }

        public async Task<IApiResponse<ApiListResponse<GlobalBrandRes>>> GetGlobalBrandsAsync(
            ApiListReq<GlobalBrandFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<GlobalBrandRes>>
            {
                Data = new ApiListResponse<GlobalBrandRes>()
            };

            var res = await _globalBrandStorage.GetGlobalBrandsAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(MapToRes);
            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<GlobalBrandRes>> GetGlobalBrandAsync(int globalBrandId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalBrandRes>();

            var globalBrand = await _globalBrandStorage.GetGlobalBrandAsync(globalBrandId, cancelToken);
            if (globalBrand == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapToRes(globalBrand);
            return response;
        }

        public async Task<IApiResponse<GlobalBrandRes>> CreateGlobalBrandAsync(CreateGlobalBrandReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalBrandRes>();

            var trimmedName = (req.Name ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(trimmedName))
                return CreateResponse(response, StatusCode.InvalidRequest, "Brand name is required.");

            // De-duplicate by name (per spec §7) within the same parent.
            var existing = await _globalBrandStorage.FindGlobalBrandByNameAsync(
                trimmedName, req.ParentGlobalBrandId, cancelToken);

            if (existing != null)
            {
                response.Data = MapToRes(existing);
                return response;
            }

            var model = MapReqToEntity(req);
            model.Name = trimmedName;
            model.Slug = BrandService.NormalizeSlug(req.Slug, fallbackName: trimmedName);
            model.IsDeleted = false;
            model.GuidId = Guid.NewGuid();
            model.CreationTime = DateTime.UtcNow;
            model.CreationUserId = AuthUser.Id;

            var created = await _globalBrandStorage.CreateGlobalBrandAsync(model, cancelToken).ConfigureAwait(false);
            if (created != null)
            {
                var refreshed = await _globalBrandStorage.GetGlobalBrandAsync(created.Id, cancelToken);
                response.Data = MapToRes(refreshed!);
            }

            return response;
        }

        public async Task<IApiResponse<GlobalBrandRes>> UpdateGlobalBrandAsync(int globalBrandId, UpdateGlobalBrandReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalBrandRes>();

            var existing = await _globalBrandStorage.GetGlobalBrandAsync(globalBrandId, cancelToken);
            if (existing == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var model = MapReqToEntity(req);
            model.Id = globalBrandId;
            model.Name = (req.Name ?? string.Empty).Trim();
            model.Slug = BrandService.NormalizeSlug(req.Slug, fallbackName: model.Name);
            model.UpdateUserId = AuthUser.Id;

            var updated = await _globalBrandStorage.UpdateGlobalBrandAsync(model, cancelToken);
            if (updated != null)
            {
                var refreshed = await _globalBrandStorage.GetGlobalBrandAsync(globalBrandId, cancelToken);
                response.Data = MapToRes(refreshed!);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteGlobalBrandAsync(int globalBrandId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>
            {
                Data = await _globalBrandStorage.DeleteGlobalBrandAsync(globalBrandId, cancelToken)
            };
            return response;
        }

        //*************************    Helpers    *************************//

        private static GlobalBrand MapReqToEntity(GlobalBrandReq req)
        {
            return new GlobalBrand
            {
                Name = req.Name,
                Slug = req.Slug,
                Description = req.Description,
                ParentGlobalBrandId = req.ParentGlobalBrandId,
                SortOrder = req.SortOrder,
                ProductCount = req.ProductCount,
                ImageUrl = req.ImageUrl,
                IconUrl = req.IconUrl,
                SeoTitle = req.SeoTitle,
                SeoDescription = req.SeoDescription,
                WooCommerceBrandId = req.WooCommerceBrandId,
            };
        }

        private static GlobalBrandRes MapToRes(GlobalBrand gb)
        {
            return new GlobalBrandRes
            {
                Id = gb.Id,
                CreationTime = gb.CreationTime,
                UpdatedDate = gb.UpdatedDate,
                CreationUserId = gb.CreationUserId,
                Name = gb.Name,
                Slug = gb.Slug,
                Description = gb.Description,
                ParentGlobalBrandId = gb.ParentGlobalBrandId,
                SortOrder = gb.SortOrder,
                ProductCount = gb.ProductCount,
                ImageUrl = gb.ImageUrl,
                IconUrl = gb.IconUrl,
                SeoTitle = gb.SeoTitle,
                SeoDescription = gb.SeoDescription,
                WooCommerceBrandId = gb.WooCommerceBrandId,
            };
        }
    }
}
