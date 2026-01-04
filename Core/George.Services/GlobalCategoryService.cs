using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class GlobalCategoryService : ServiceBase
    {
        private readonly GlobalCategoryStorage _globalCategoryStorage;

        public GlobalCategoryService(
            ILogger<GlobalCategoryService> logger,
            IMapper mapper,
            CacheManager cache,
            GlobalCategoryStorage globalCategoryStorage
        ) : base(logger, mapper, cache)
        {
            _globalCategoryStorage = globalCategoryStorage;
        }

        public async Task<IApiResponse<ApiListResponse<GlobalCategoryRes>>> GetGlobalCategoriesAsync(
            ApiListReq<GlobalCategoryFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<GlobalCategoryRes>>
            {
                Data = new ApiListResponse<GlobalCategoryRes>()
            };

            var res = await _globalCategoryStorage.GetGlobalCategoriesAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(gc => MapGlobalCategoryToRes(gc));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<GlobalCategoryRes>> GetGlobalCategoryAsync(int globalCategoryId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalCategoryRes>();

            var globalCategory = await _globalCategoryStorage.GetGlobalCategoryAsync(globalCategoryId, cancelToken);
            if (globalCategory == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapGlobalCategoryToRes(globalCategory);
            return response;
        }

        public async Task<IApiResponse<GlobalCategoryRes>> CreateGlobalCategoryAsync(CreateGlobalCategoryReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalCategoryRes>();

            // Convert to EF model
            var globalCategory = MapReqToGlobalCategory(req);
            globalCategory.CreationUserId = AuthUser.Id;
            globalCategory.CreationTime = DateTime.UtcNow;
            globalCategory.IsDeleted = false;

            // Create the data in the DB
            globalCategory = await _globalCategoryStorage.CreateGlobalCategoryAsync(globalCategory, req.BusinessTypeIds, cancelToken).ConfigureAwait(false);
            
            if (globalCategory != null)
            {
                // Load with relationships for mapping
                globalCategory = await _globalCategoryStorage.GetGlobalCategoryAsync(globalCategory.Id, cancelToken);
                // Convert to response
                response.Data = MapGlobalCategoryToRes(globalCategory!);
            }

            return response;
        }

        public async Task<IApiResponse<GlobalCategoryRes>> UpdateGlobalCategoryAsync(int globalCategoryId, UpdateGlobalCategoryReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<GlobalCategoryRes>();

            var existingGlobalCategory = await _globalCategoryStorage.GetGlobalCategoryAsync(globalCategoryId, cancelToken);
            if (existingGlobalCategory == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Map request to DB model
            var globalCategory = MapReqToGlobalCategory(req);
            globalCategory.Id = globalCategoryId;
            globalCategory.UpdateUserId = AuthUser.Id;

            // Update global category
            globalCategory = await _globalCategoryStorage.UpdateGlobalCategoryAsync(globalCategory, req.BusinessTypeIds, cancelToken);

            if (globalCategory != null)
            {
                // Reload with all relationships
                globalCategory = await _globalCategoryStorage.GetGlobalCategoryAsync(globalCategoryId, cancelToken);
                response.Data = MapGlobalCategoryToRes(globalCategory!);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteGlobalCategoryAsync(int globalCategoryId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _globalCategoryStorage.DeleteGlobalCategoryAsync(globalCategoryId, cancelToken);
            response.Data = result;

            return response;
        }

        // Helper methods
        private GlobalCategory MapReqToGlobalCategory(GlobalCategoryReq req)
        {
            return new GlobalCategory
            {
                Name = req.Name,
                Description = req.Description,
                ParentGlobalCategoryId = req.ParentGlobalCategoryId,
                SortOrder = req.SortOrder,
                ProductCount = req.ProductCount
            };
        }

        private GlobalCategoryRes MapGlobalCategoryToRes(GlobalCategory globalCategory)
        {
            var res = new GlobalCategoryRes
            {
                Id = globalCategory.Id,
                CreationTime = globalCategory.CreationTime,
                UpdatedDate = globalCategory.UpdatedDate,
                CreationUserId = globalCategory.CreationUserId,
                Name = globalCategory.Name,
                Description = globalCategory.Description,
                ParentGlobalCategoryId = globalCategory.ParentGlobalCategoryId,
                SortOrder = globalCategory.SortOrder,
                ProductCount = globalCategory.ProductCount
            };

            // Map business types
            if (globalCategory.BusinessTypes != null && globalCategory.BusinessTypes.Any())
            {
                res.BusinessTypeIds = globalCategory.BusinessTypes.Select(bt => bt.Id).ToList();
            }

            return res;
        }
    }
}

