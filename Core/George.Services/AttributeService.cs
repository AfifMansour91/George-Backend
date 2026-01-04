using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.Data.Models;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;
using Attribute = George.DB.Attribute;

namespace George.Services
{
    public class AttributeService : ServiceBase
    {
        private readonly AttributeStorage _attributeStorage;

        public AttributeService(
            ILogger<AttributeService> logger,
            IMapper mapper,
            CacheManager cache,
            AttributeStorage attributeStorage
        ) : base(logger, mapper, cache)
        {
            _attributeStorage = attributeStorage;
        }

        public async Task<IApiResponse<ApiListResponse<AttributeRes>>> GetAttributesAsync(
            ApiListReq<AttributeFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<AttributeRes>>
            {
                Data = new ApiListResponse<AttributeRes>()
            };

            var res = await _attributeStorage.GetAttributesAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(a => MapAttributeToRes(a));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<AttributeRes>> GetAttributeAsync(int attributeId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AttributeRes>();

            var attribute = await _attributeStorage.GetAttributeAsync(attributeId, cancelToken);
            if (attribute == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapAttributeToRes(attribute);

            return response;
        }

        public async Task<IApiResponse<AttributeRes>> CreateAttributeAsync(CreateAttributeReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AttributeRes>();

            // Convert to EF model
            Attribute? model = _mapper.Map<Attribute>(req);
            model.CreationUserId = AuthUser.Id;
            model.CreationTime = DateTime.UtcNow;
            model.IsDeleted = false;

            // Create the data in the DB.
            model = await _attributeStorage.CreateAttributeAsync(model, req.Values, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with relationships for mapping
                model = await _attributeStorage.GetAttributeAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = MapAttributeToRes(model);
            }

            return response;
        }

        public async Task<IApiResponse<AttributeRes>> UpdateAttributeAsync(int attributeId, UpdateAttributeReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<AttributeRes>();

            var existingAttribute = await _attributeStorage.GetAttributeAsync(attributeId, cancelToken);
            if (existingAttribute == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Convert to EF model
            Attribute? model = _mapper.Map<Attribute>(req);
            model.Id = attributeId;
            model.UpdateUserId = AuthUser.Id;

            // Update the data in the DB.
            model = await _attributeStorage.UpdateAttributeAsync(model, req.Values, cancelToken).ConfigureAwait(false);
            if (model != null)
            {
                // Load with relationships for mapping
                model = await _attributeStorage.GetAttributeAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = MapAttributeToRes(model);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteAttributeAsync(int attributeId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _attributeStorage.DeleteAttributeAsync(attributeId, cancelToken);
            response.Data = result;

            return response;
        }

        private AttributeRes MapAttributeToRes(Attribute attribute)
        {
            var res = new AttributeRes
            {
                Id = attribute.Id,
                CreationTime = attribute.CreationTime,
                UpdatedDate = attribute.UpdatedDate,
                CreationUserId = attribute.CreationUserId,
                Name = attribute.Name,
                SiteId = attribute.SiteId
            };

            // Map attribute values
            if (attribute.AttributeValues != null && attribute.AttributeValues.Any())
            {
                res.Values = attribute.AttributeValues.Select(av => av.Value).ToList();
            }

            return res;
        }
    }
}