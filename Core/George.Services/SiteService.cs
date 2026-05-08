using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class SiteService : ServiceBase
    {
        private readonly SiteStorage _siteStorage;

        public SiteService(
            ILogger<SiteService> logger,
            IMapper mapper,
            CacheManager cache,
            SiteStorage siteStorage
        ) : base(logger, mapper, cache)
        {
            _siteStorage = siteStorage;
        }

        public async Task<IApiResponse<ApiListResponse<SiteRes>>> GetSitesAsync(
            ApiListReq<SiteFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<SiteRes>>
            {
                Data = new ApiListResponse<SiteRes>()
            };

            var res = await _siteStorage.GetSitesAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(a => _mapper.Map<SiteRes>(a));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<SiteRes>> CreateSiteAsync(CreateSiteReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<SiteRes>();

            // Convert to EF model
            Site? model = _mapper.Map<Site>(req);
            model.CreationUserId = AuthUser.Id;
            model.CreationTime = DateTime.UtcNow;
            model.IsActive = true;
            if (!model.IsraelCityPickerEnabled.HasValue)
                model.IsraelCityPickerEnabled = true;
            if (!model.AskBagsCountAtPickingFinish.HasValue)
                model.AskBagsCountAtPickingFinish = true;

            // Create the data in the DB.
            model = await _siteStorage.CreateSiteAsync(model, req.BusinessTypeIds, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with business types for mapping
                model = await _siteStorage.GetSiteAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = _mapper.Map<SiteRes>(model);
            }

            return response;
        }

        // 2. Get site details (after created)
        public async Task<IApiResponse<SiteRes>> GetSiteAsync(int siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<SiteRes>();

            var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (site == null)
                return CreateResponse(response, StatusCode.ItemNotFound);


            response.Data = _mapper.Map<SiteRes>(site);

            return response;
        }

        // 3. Update site settings (kosher/weighted/shop name)
        // Note: siteId comes from route parameter, ensuring updates are always by ID, not by name
        public async Task<IApiResponse<SiteRes>> UpdateSiteAsync(int siteId, UpdateSiteReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<SiteRes>();

            // Get existing site to preserve AccountId if not provided
            // Always use the ID from the route parameter, not from the request body
            var existingSite = await _siteStorage.GetSiteAsync(siteId, cancelToken);
            if (existingSite == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Convert to EF model
            Site? model = _mapper.Map<Site>(req);
            // Always use the ID from the route parameter to ensure updates are by ID, not by name
            model.Id = siteId;
            model.UpdateUserId = AuthUser.Id;
            
            // Preserve AccountId from existing site if not provided in request
            if (model.AccountId == 0)
            {
                model.AccountId = existingSite.AccountId;
            }

            // Update the data in the DB.
            model = await _siteStorage.UpdateSiteAsync(model, req.BusinessTypeIds, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with business types for mapping
                model = await _siteStorage.GetSiteAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = _mapper.Map<SiteRes>(model);
            }

            return response;
        }

        public async Task<IApiResponse<List<SiteRes>>> GetSitesByAccountAsync(int accountId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<SiteRes>>();

            var sites = await _siteStorage.GetSitesByAccountAsync(accountId, cancelToken);
            response.Data = sites.ConvertAll(s => _mapper.Map<SiteRes>(s));

            return response;
        }

        public async Task<IApiResponse<SiteRes?>> DeleteSiteAsync(int id, CancellationToken cancelToken = default)
        {
            IApiResponse<SiteRes?> response = new ApiResponse<SiteRes?>();

            //// Verify authorization.

            //// Check for dependencies.
            //if (await _taskStorage.TaskHasDependenciesAsync(id))
            //	return CreateResponse(response, StatusCode.ItemHasDependencies);

            // Delete from the DB.
            Site? model = await _siteStorage.DeleteSiteAsync(id, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Convert to response.
                response.Data = _mapper.Map<SiteRes>(model);
            }

            return response;
        }

    }

}
