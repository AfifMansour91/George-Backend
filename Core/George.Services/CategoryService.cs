using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class CategoryService : ServiceBase
    {
        private readonly CategoryStorage _categoryStorage;

        public CategoryService(
            ILogger<CategoryService> logger,
            IMapper mapper,
            CacheManager cache,
            CategoryStorage categoryStorage
        ) : base(logger, mapper, cache)
        {
            _categoryStorage = categoryStorage;
        }

        public async Task<IApiResponse<ApiListResponse<CategoryRes>>> GetCategoriesAsync(
            ApiListReq<CategoryFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<CategoryRes>>
            {
                Data = new ApiListResponse<CategoryRes>()
            };

            var res = await _categoryStorage.GetCategoriesAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(c => MapCategoryToRes(c));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<CategoryRes>> GetCategoryAsync(int categoryId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CategoryRes>();

            var category = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
            if (category == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapCategoryToRes(category);

            return response;
        }

        public async Task<IApiResponse<CategoryRes>> CreateCategoryAsync(CreateCategoryReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CategoryRes>();

            // Convert to EF model
            Category? model = _mapper.Map<Category>(req);
            model.CreationUserId = AuthUser.Id;
            model.CreationTime = DateTime.UtcNow;
            model.IsActive = true;
            model.IsDeleted = false;

            // Create the data in the DB.
            model = await _categoryStorage.CreateCategoryAsync(model, req.SiteIds, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with relationships for mapping
                model = await _categoryStorage.GetCategoryAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = MapCategoryToRes(model);
            }

            return response;
        }

        public async Task<IApiResponse<CategoryRes>> UpdateCategoryAsync(int categoryId, UpdateCategoryReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<CategoryRes>();

            var existingCategory = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
            if (existingCategory == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Convert to EF model
            Category? model = _mapper.Map<Category>(req);
            model.Id = categoryId;
            model.UpdateUserId = AuthUser.Id;

            // Update the data in the DB.
            model = await _categoryStorage.UpdateCategoryAsync(model, req.SiteIds, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with relationships for mapping
                model = await _categoryStorage.GetCategoryAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = MapCategoryToRes(model);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteCategoryAsync(int categoryId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _categoryStorage.DeleteCategoryAsync(categoryId, cancelToken);
            response.Data = result;

            return response;
        }

        private CategoryRes MapCategoryToRes(Category category)
        {
            var res = new CategoryRes
            {
                Id = category.Id,
                CreationTime = category.CreationTime,
                UpdatedDate = category.UpdatedDate,
                CreationUserId = category.CreationUserId,
                Name = category.Name,
                ParentCategoryId = category.ParentCategoryId,
                Description = category.Description,
                CustomName = category.CustomName,
                IsEnabled = category.IsEnabled,
                SortOrder = category.SortOrder,
                DisplayAsMain = category.DisplayAsMain,
                AccountId = category.AccountId
            };

            // Map sites
            if (category.Sites != null && category.Sites.Any())
            {
                res.SiteIds = category.Sites.Select(s => s.Id).ToList();
            }

            return res;
        }
    }
}
