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
    public class MediaService : ServiceBase
    {
        private readonly MediaStorage _mediaStorage;

        public MediaService(
            ILogger<MediaService> logger,
            IMapper mapper,
            CacheManager cache,
            MediaStorage mediaStorage
        ) : base(logger, mapper, cache)
        {
            _mediaStorage = mediaStorage;
        }

        public async Task<IApiResponse<ApiListResponse<MediaRes>>> GetMediaAsync(
            ApiListReq<MediaFilter> request,
            CancellationToken cancelToken)
        {
            var response = new ApiResponse<ApiListResponse<MediaRes>>
            {
                Data = new ApiListResponse<MediaRes>()
            };

            var res = await _mediaStorage.GetMediaAsync(request.Filter, request, cancelToken);

            response.Data!.Items = res.Items.ConvertAll(m => MapMediaToRes(m));

            response.Data.Skip = request.Skip;
            response.Data.Limit = request.Take;
            response.Data.Total = res.Total;

            return response;
        }

        public async Task<IApiResponse<MediaRes>> GetMediaAsync(int mediaId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<MediaRes>();

            var media = await _mediaStorage.GetMediaAsync(mediaId, cancelToken);
            if (media == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            response.Data = MapMediaToRes(media);
            return response;
        }

        public async Task<IApiResponse<MediaRes>> CreateMediaAsync(CreateMediaReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<MediaRes>();

            // Convert to EF model
            var media = MapReqToMedia(req);
            media.CreationUserId = AuthUser.Id;
            media.CreationTime = DateTime.UtcNow;
            media.IsDeleted = false;

            // Get or create MediaType
            if (req.Type.HasValue())
            {
                media.TypeId = await _mediaStorage.GetOrCreateMediaTypeAsync(req.Type, cancelToken);
            }

            // Combine category IDs
            var categoryIds = CombineCategoryIds(req.CategoryIds, req.SubcategoryIds);

            // Create the data in the DB (accountIdForTags used for per-account tag lookup)
            media = await _mediaStorage.CreateMediaAsync(media, categoryIds, req.Tags, req.AccountId, cancelToken).ConfigureAwait(false);
            
            if (media != null)
            {
                // When account uploads media, record usage so it appears in AccountMedia (consistent with "Add to my media")
                if (req.AccountId.HasValue)
                {
                    await _mediaStorage.AddAccountMediaUsageAsync(req.AccountId.Value, media.Id, cancelToken).ConfigureAwait(false);
                }
                // Load with relationships for mapping
                media = await _mediaStorage.GetMediaAsync(media.Id, cancelToken);
                // Convert to response
                response.Data = MapMediaToRes(media!);
            }

            return response;
        }

        public async Task<IApiResponse<MediaRes>> UpdateMediaAsync(int mediaId, UpdateMediaReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<MediaRes>();

            var existingMedia = await _mediaStorage.GetMediaAsync(mediaId, cancelToken);
            if (existingMedia == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            // Map request to DB model
            var media = MapReqToMedia(req);
            media.Id = mediaId;
            media.UpdateUserId = AuthUser.Id;

            // Get or create MediaType
            if (req.Type.HasValue())
            {
                media.TypeId = await _mediaStorage.GetOrCreateMediaTypeAsync(req.Type, cancelToken);
            }

            // Combine category IDs
            var categoryIds = CombineCategoryIds(req.CategoryIds, req.SubcategoryIds);

            // Update media (accountIdForTags used for per-account tag lookup)
            media = await _mediaStorage.UpdateMediaAsync(media, categoryIds, req.Tags, req.AccountId, cancelToken);

            if (media != null)
            {
                // Reload with all relationships
                media = await _mediaStorage.GetMediaAsync(mediaId, cancelToken);
                response.Data = MapMediaToRes(media!);
            }

            return response;
        }

        public async Task<IApiResponse<bool>> DeleteMediaAsync(int mediaId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _mediaStorage.DeleteMediaAsync(mediaId, cancelToken);
            response.Data = result;

            return response;
        }

        /// <summary>Record that an account uses a media item (idempotent).</summary>
        public async Task<IApiResponse<bool>> UseMediaAsync(int mediaId, UseMediaReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var media = await _mediaStorage.GetMediaAsync(mediaId, cancelToken);
            if (media == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            await _mediaStorage.AddAccountMediaUsageAsync(req.AccountId, mediaId, cancelToken).ConfigureAwait(false);
            response.Data = true;
            return response;
        }

        // Helper methods
        private Medium MapReqToMedia(MediaReq req)
        {
            return new Medium
            {
                Url = req.Url,
                Name = req.Name,
                BusinessTypeId = req.BusinessTypeId,
                FileSize = req.FileSize,
                UsageCount = req.UsageCount
            };
        }

        private MediaRes MapMediaToRes(Medium media)
        {
            var res = new MediaRes
            {
                Id = media.Id,
                CreationTime = media.CreationTime,
                UpdatedDate = media.UpdatedDate,
                CreationUserId = media.CreationUserId,
                Url = media.Url,
                Name = media.Name,
                BusinessTypeId = media.BusinessTypeId,
                FileSize = media.FileSize,
                UsageCount = media.UsageCount
            };

            // Map type
            if (media.Type != null)
            {
                res.Type = media.Type.Name;
            }

            // Map categories (separate main categories from subcategories)
            if (media.Categories != null && media.Categories.Any())
            {
                var mainCategories = media.Categories
                    .Where(c => c.ParentCategoryId == null)
                    .Select(c => c.Id)
                    .ToList();
                var subCategories = media.Categories
                    .Where(c => c.ParentCategoryId != null)
                    .Select(c => c.Id)
                    .ToList();
                
                res.CategoryIds = mainCategories;
                res.SubcategoryIds = subCategories;
            }

            // Map tags
            if (media.Tags != null && media.Tags.Any())
            {
                res.Tags = media.Tags.Select(t => t.Name).ToList();
            }

            return res;
        }

        private List<int> CombineCategoryIds(List<int>? categoryIds, List<int>? subcategoryIds)
        {
            var combined = new List<int>();
            if (categoryIds != null) combined.AddRange(categoryIds);
            if (subcategoryIds != null) combined.AddRange(subcategoryIds);
            return combined;
        }
    }
}

