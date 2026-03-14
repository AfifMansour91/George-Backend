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
        private readonly WooCommerceService _wooCommerceService;

        public CategoryService(
            ILogger<CategoryService> logger,
            IMapper mapper,
            CacheManager cache,
            CategoryStorage categoryStorage,
            WooCommerceService wooCommerceService
        ) : base(logger, mapper, cache)
        {
            _categoryStorage = categoryStorage;
            _wooCommerceService = wooCommerceService;
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
            model!.CreationUserId = AuthUser.Id;
            model.CreationTime = DateTime.UtcNow;
            model.IsActive = true;
            model.IsDeleted = false;
            model.ShowInKiosk = req.ShowInKiosk ?? true;

            // Create the data in the DB.
            model = await _categoryStorage.CreateCategoryAsync(model, req.SiteIds, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with relationships for mapping
                model = await _categoryStorage.GetCategoryAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = MapCategoryToRes(model);

                // Sync to WooCommerce if enabled for any linked sites
                if (model.Site != null && model.Site.Any())
                {
                    await SyncCategoryToWooCommerceForEnabledSitesAsync(model.Id, model.Site, cancelToken);
                }
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

                // Sync to WooCommerce if enabled for any linked sites
                if (model.Site != null && model.Site.Any())
                {
                    await SyncCategoryToWooCommerceForEnabledSitesAsync(model.Id, model.Site, cancelToken);
                }
            }

            return response;
        }

        /// <param name="siteId">When provided, only removes the category from this site (unlinks CategorySite). Other sites keep the category. When null, soft-deletes the category for all sites.</param>
        public async Task<IApiResponse<bool>> DeleteCategoryAsync(int categoryId, int? siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            if (siteId.HasValue)
            {
                // Remove category from this site only; do not remove from other sites
                var category = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
                if (category != null && category.WooCommerceId.HasValue && category.Site != null)
                {
                    var site = category.Site.FirstOrDefault(s => s.Id == siteId.Value && s.WooCommerceEnabled == true);
                    if (site != null)
                    {
                        try
                        {
                            var deleted = await _wooCommerceService.DeleteCategoryFromWooCommerceAsync(site.Id, category.WooCommerceId.Value, cancelToken);
                            if (deleted)
                                _logger.LogInformation("Removed category {CategoryId} (WooCommerce id {WooId}) from WooCommerce for site {SiteId}", categoryId, category.WooCommerceId, site.Id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error removing category {CategoryId} from WooCommerce for site {SiteId}", categoryId, site.Id);
                        }
                    }
                }
                var result = await _categoryStorage.RemoveCategoryFromSiteAsync(categoryId, siteId.Value, cancelToken);
                response.Data = result;
                return response;
            }

            // Full delete: soft-delete category and remove from WooCommerce for all linked sites
            var categoryForDelete = await _categoryStorage.GetCategoryAsync(categoryId, cancelToken);
            if (categoryForDelete != null && categoryForDelete.WooCommerceId.HasValue && categoryForDelete.Site != null && categoryForDelete.Site.Any())
            {
                foreach (var site in categoryForDelete.Site.Where(s => s.WooCommerceEnabled == true))
                {
                    try
                    {
                        var deleted = await _wooCommerceService.DeleteCategoryFromWooCommerceAsync(site.Id, categoryForDelete.WooCommerceId.Value, cancelToken);
                        if (deleted)
                            _logger.LogInformation("Deleted category {CategoryId} (WooCommerce id {WooId}) from WooCommerce for site {SiteId}", categoryId, categoryForDelete.WooCommerceId, site.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting category {CategoryId} from WooCommerce for site {SiteId}", categoryId, site.Id);
                    }
                }
            }

            var deleteResult = await _categoryStorage.DeleteCategoryAsync(categoryId, cancelToken);
            response.Data = deleteResult;

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
                AccountId = category.AccountId,
                ImageUrl = category.ImageUrl,
                IconUrl = category.IconUrl,
                ShowInKiosk = category.ShowInKiosk,
                KioskDisplayOrder = category.KioskDisplayOrder
            };

            // Map sites
            if (category.Site != null && category.Site.Any())
            {
                res.SiteIds = category.Site.Select(s => s.Id).ToList();
            }

            return res;
        }

        /// <summary>
        /// Syncs a category to WooCommerce for all enabled sites
        /// </summary>
        private async Task SyncCategoryToWooCommerceForEnabledSitesAsync(
            int categoryId,
            ICollection<Site> sites,
            CancellationToken cancelToken)
        {
            // Find sites with WooCommerce enabled
            var enabledSites = sites.Where(s => s.WooCommerceEnabled == true).ToList();

            if (!enabledSites.Any())
                return;

            // Sync to each enabled site
            // Catch errors so they don't block the category create/update operation
            foreach (var site in enabledSites)
            {
                try
                {
                    var syncResponse = await _wooCommerceService.SyncCategoryToWooCommerceAsync(
                        categoryId,
                        site.Id,
                        cancelToken);

                    if (!syncResponse.Data?.Success == true)
                    {
                        _logger.LogWarning(
                            "Failed to sync category {CategoryId} to WooCommerce for site {SiteId}: {Message}",
                            categoryId, site.Id, syncResponse.Data?.Message ?? "Unknown error");
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Successfully synced category {CategoryId} to WooCommerce for site {SiteId}",
                            categoryId, site.Id);
                    }
                }
                catch (Exception ex)
                {
                    // Log error but don't throw - we don't want WooCommerce sync failures to block category operations
                    _logger.LogError(ex, 
                        "Error syncing category {CategoryId} to WooCommerce for site {SiteId}", 
                        categoryId, site.Id);
                }
            }
        }
    }
}
