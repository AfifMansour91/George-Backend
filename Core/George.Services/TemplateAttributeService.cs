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
    public class TemplateAttributeService : ServiceBase
    {
        private readonly TemplateAttributeStorage _templateAttributeStorage;

        public TemplateAttributeService(
            ILogger<TemplateAttributeService> logger,
            IMapper mapper,
            CacheManager cache,
            TemplateAttributeStorage templateAttributeStorage
        ) : base(logger, mapper, cache)
        {
            _templateAttributeStorage = templateAttributeStorage;
        }

        public async Task<IApiResponse<ApiListResponse<TemplateAttributeRes>>> GetTemplateAttributesAsync(
            ApiListReq<TemplateAttributeFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<TemplateAttributeRes>>
            {
                Data = new ApiListResponse<TemplateAttributeRes>()
            };

            var res = await _templateAttributeStorage.GetTemplateAttributesAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(ta => MapTemplateAttributeToRes(ta));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<TemplateAttributeRes>> GetTemplateAttributeAsync(int templateAttributeId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<TemplateAttributeRes>();

            var templateAttribute = await _templateAttributeStorage.GetTemplateAttributeAsync(templateAttributeId, cancelToken);
            if (templateAttribute == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapTemplateAttributeToRes(templateAttribute);
            return response;
        }

        public async Task<IApiResponse<TemplateAttributeRes>> CreateTemplateAttributeAsync(CreateTemplateAttributeReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<TemplateAttributeRes>();

            // Convert to EF model
            var templateAttribute = MapReqToTemplateAttribute(req);
            templateAttribute.CreationUserId = AuthUser.Id;
            templateAttribute.CreationTime = DateTime.UtcNow;
            templateAttribute.IsDeleted = false;

            // Create the data in the DB
            templateAttribute = await _templateAttributeStorage.CreateTemplateAttributeAsync(
                templateAttribute, 
                req.Values, 
                req.SiteIds, 
                cancelToken).ConfigureAwait(false);
            
            if (templateAttribute != null)
            {
                // Load with relationships for mapping
                templateAttribute = await _templateAttributeStorage.GetTemplateAttributeAsync(templateAttribute.Id, cancelToken);
                // Convert to response
                response.Data = MapTemplateAttributeToRes(templateAttribute!);
            }

            return response;
        }

        public async Task<IApiResponse<TemplateAttributeRes>> UpdateTemplateAttributeAsync(int templateAttributeId, UpdateTemplateAttributeReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<TemplateAttributeRes>();

            var existingTemplateAttribute = await _templateAttributeStorage.GetTemplateAttributeAsync(templateAttributeId, cancelToken);
            if (existingTemplateAttribute == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Map request to DB model
            var templateAttribute = MapReqToTemplateAttribute(req);
            templateAttribute.Id = templateAttributeId;
            templateAttribute.UpdateUserId = AuthUser.Id;

            // Update template attribute
            templateAttribute = await _templateAttributeStorage.UpdateTemplateAttributeAsync(
                templateAttribute, 
                req.Values, 
                req.SiteIds, 
                cancelToken);

            if (templateAttribute != null)
            {
                // Reload with all relationships
                templateAttribute = await _templateAttributeStorage.GetTemplateAttributeAsync(templateAttributeId, cancelToken);
                response.Data = MapTemplateAttributeToRes(templateAttribute!);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteTemplateAttributeAsync(int templateAttributeId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _templateAttributeStorage.DeleteTemplateAttributeAsync(templateAttributeId, cancelToken);
            response.Data = result;

            return response;
        }

        // Helper methods
        private TemplateAttribute MapReqToTemplateAttribute(TemplateAttributeReq req)
        {
            return new TemplateAttribute
            {
                Name = req.Name
            };
        }

        private TemplateAttributeRes MapTemplateAttributeToRes(TemplateAttribute templateAttribute)
        {
            var res = new TemplateAttributeRes
            {
                Id = templateAttribute.Id,
                CreationTime = templateAttribute.CreationTime,
                UpdatedDate = templateAttribute.UpdatedDate,
                CreationUserId = templateAttribute.CreationUserId,
                Name = templateAttribute.Name
            };

            // Map values
            if (templateAttribute.TemplateAttributeValues != null && templateAttribute.TemplateAttributeValues.Any())
            {
                res.Values = templateAttribute.TemplateAttributeValues.Select(tav => tav.Value).ToList();
            }

            // Map sites
            if (templateAttribute.Sites != null && templateAttribute.Sites.Any())
            {
                res.SiteIds = templateAttribute.Sites.Select(s => s.Id).ToList();
            }

            return res;
        }
    }
}

