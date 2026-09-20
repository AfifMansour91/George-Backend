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
        private readonly WooCommerceService _wooCommerceService;
        private readonly SiteStorage _siteStorage;

        public AttributeService(
            ILogger<AttributeService> logger,
            IMapper mapper,
            CacheManager cache,
            AttributeStorage attributeStorage,
            WooCommerceService wooCommerceService,
            SiteStorage siteStorage
        ) : base(logger, mapper, cache)
        {
            _attributeStorage = attributeStorage;
            _wooCommerceService = wooCommerceService;
            _siteStorage = siteStorage;
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
            // The typed order of several values is the user's own order (e.g. קטן, בינוני, גדול); a single value carries none.
            model = await _attributeStorage.CreateAttributeAsync(model, req.Values, cancelToken, preserveValueOrder: req.Values?.Count > 1).ConfigureAwait(false);
            if (model != null)
            {
                // Load with relationships for mapping
                model = await _attributeStorage.GetAttributeAsync(model.Id, cancelToken);
                // Convert to response.
                response.Data = MapAttributeToRes(model);

                // Sync to WooCommerce if enabled for the site
                await SyncAttributeToWooCommerceWhenEnabledAsync(model.Id, model.SiteId, cancelToken);
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
            if (attribute.AttributeValue != null && attribute.AttributeValue.Any())
            {
                res.Values = OrderedValues(attribute);
            }

            return res;
        }

        /// <summary>Attribute values in their manual order; never-ordered values (NULL) follow alphabetically.</summary>
        public static List<string> OrderedValues(Attribute attribute)
        {
            return attribute.AttributeValue
                .OrderBy(av => av.DisplayOrder ?? int.MaxValue)
                .ThenBy(av => av.Value, StringComparer.OrdinalIgnoreCase)
                .Select(av => av.Value)
                .ToList();
        }

        /// <summary>Linked-product count per attribute value for a site (attributes screen). Values with no products are omitted.</summary>
        public async Task<IApiResponse<List<AttributeValueProductCountRes>>> GetValueProductCountsAsync(int siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<List<AttributeValueProductCountRes>> { Data = new List<AttributeValueProductCountRes>() };

            var attributes = await _attributeStorage.GetAttributesAsync(
                new AttributeFilter { SiteIds = new List<int> { siteId } }, new PagingExDto(), cancelToken);
            var links = await _attributeStorage.GetSiteProductOptionValueLinksAsync(siteId, cancelToken);

            // Products link to attributes by option NAME + value text (no FK), compared like the rest of the option code: trimmed, case-insensitive.
            var productIdsByKey = links
                .GroupBy(l => (Name: l.OptionName.ToLowerInvariant(), Value: l.Value.ToLowerInvariant()))
                .ToDictionary(g => g.Key, g => g.Select(l => l.ProductId).Distinct().Count());

            foreach (var attribute in attributes.Items.Where(a => !a.IsDeleted))
            {
                var name = attribute.Name?.Trim().ToLowerInvariant() ?? "";
                foreach (var av in attribute.AttributeValue)
                {
                    var value = av.Value?.Trim().ToLowerInvariant() ?? "";
                    if (productIdsByKey.TryGetValue((name, value), out var count) && count > 0)
                        response.Data.Add(new AttributeValueProductCountRes { AttributeId = attribute.Id, Value = av.Value!, ProductCount = count });
                }
            }

            return response;
        }

        private async Task SyncAttributeToWooCommerceWhenEnabledAsync(int attributeId, int siteId, CancellationToken cancelToken)
        {
            try
            {
                var site = await _siteStorage.GetSiteAsync(siteId, cancelToken);
                if (site?.WooCommerceEnabled != true) return;

                var syncRes = await _wooCommerceService.SyncAttributeToWooCommerceAsync(attributeId, siteId, cancelToken);
                if (syncRes.Data?.Success == true)
                    _logger.LogInformation("Synced attribute {AttributeId} to WooCommerce for site {SiteId}", attributeId, siteId);
                else
                    _logger.LogWarning("Failed to sync attribute {AttributeId} to WooCommerce for site {SiteId}: {Message}", attributeId, siteId, syncRes.Data?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing attribute {AttributeId} to WooCommerce for site {SiteId}", attributeId, siteId);
            }
        }
    }
}