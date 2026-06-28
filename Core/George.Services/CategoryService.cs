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
        private readonly UserStorage _userStorage;

        public CategoryService(
            ILogger<CategoryService> logger,
            IMapper mapper,
            CacheManager cache,
            CategoryStorage categoryStorage,
            WooCommerceService wooCommerceService,
            UserStorage userStorage
        ) : base(logger, mapper, cache)
        {
            _categoryStorage = categoryStorage;
            _wooCommerceService = wooCommerceService;
            _userStorage = userStorage;
        }

        public async Task<IApiResponse<ApiListResponse<CategoryRes>>> GetCategoriesAsync(
            ApiListReq<CategoryFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<CategoryRes>>
            {
                Data = new ApiListResponse<CategoryRes>()
            };

            // Safety net: an account-scoped user may only ever see their OWN account's categories, regardless
            // of what the client sends. Without this the list endpoint returned categories from EVERY account when
            // the client omitted AccountId. Master AND system-admin (UserRole.Admin / super-admin) users are
            // unrestricted: they browse and impersonate any account, so we must NOT pin them to their home account
            // — doing so overrode the impersonated AccountId the client sent and hid the impersonated account's
            // categories. Same isMaster-or-Admin rule used by DashboardService / SiteAccessService.
            if (!AuthUser.IsMaster && AuthUser.Id > 0)
            {
                var user = await _userStorage.GetUserAsync(AuthUser.Id, cancelToken);
                if (user?.AccountId != null && user.RoleId != (int)UserRole.Admin)
                {
                    request.Filter ??= new CategoryFilter();
                    request.Filter.AccountId = user.AccountId.Value;
                }
            }

            var res = await _categoryStorage.GetCategoriesAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(c => MapCategoryToRes(c));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        // Categories are always account-scoped, but the create/edit client never sends AccountId. Derive it —
        // preferring an explicit request value, then a known fallback (e.g. the existing row's account on update),
        // then the signed-in user's account — so the row is never persisted with AccountId = NULL. A NULL-account
        // category is hidden by the account-scoped read filter in GetCategoriesAsync and skipped by per-site
        // WooCommerce sync, so it would never appear in the product create/edit category picker.
        private async Task EnsureAccountIdAsync(Category model, CancellationToken cancelToken, int? fallbackAccountId = null)
        {
            if (model.AccountId.HasValue) return;

            if (fallbackAccountId.HasValue)
            {
                model.AccountId = fallbackAccountId;
                return;
            }

            if (AuthUser.Id > 0)
            {
                var user = await _userStorage.GetUserAsync(AuthUser.Id, cancelToken);
                model.AccountId = user?.AccountId;
            }
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

            // The category client doesn't send AccountId, so derive it from the signed-in user. Without this the row
            // is saved with AccountId = NULL, which makes it invisible to the account-scoped category list (see the
            // safety net in GetCategoriesAsync) and skips per-site WooCommerce sync, so it never shows up in the
            // product create/edit category picker. It also lets CategoryStorage auto-link the category to all of the
            // account's sites when no explicit SiteIds are given.
            await EnsureAccountIdAsync(model, cancelToken).ConfigureAwait(false);

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

            // UpdateCategoryAsync overwrites AccountId with the mapped value, but the client doesn't send it. Keep the
            // existing account scope (and repair legacy rows that were saved with AccountId = NULL by falling back to
            // the signed-in user's account) so the edit doesn't null it out and re-hide the category.
            await EnsureAccountIdAsync(model, cancelToken, existingCategory.AccountId).ConfigureAwait(false);

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
