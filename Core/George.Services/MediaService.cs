using System.Linq;
using System.Net.Http.Headers;
using AutoMapper;
using George.Common;
using George.Common.Request;
using George.Data;
using George.DB;
using George.Services.Request;
using George.Services.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    public class MediaService : ServiceBase
    {
        private readonly MediaStorage _mediaStorage;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IFileStorage _fileStorage;

        public MediaService(
            ILogger<MediaService> logger,
            IMapper mapper,
            CacheManager cache,
            MediaStorage mediaStorage,
            IHttpClientFactory httpClientFactory,
            IFileStorage fileStorage
        ) : base(logger, mapper, cache)
        {
            _mediaStorage = mediaStorage;
            _httpClientFactory = httpClientFactory;
            _fileStorage = fileStorage;
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

            // Prefer client-provided name. Only derive from URL when name is missing (so uploads keep their file name, not a storage GUID).
            var nameToUse = req.Name;
            if (string.IsNullOrWhiteSpace(nameToUse) && req.Url != null && (req.Url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || req.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                var fileNameFromUrl = GetFileNameFromUrl(req.Url);
                if (!string.IsNullOrWhiteSpace(fileNameFromUrl))
                    nameToUse = fileNameFromUrl;
            }
            if (string.IsNullOrWhiteSpace(nameToUse))
                nameToUse = "image";

            // Convert to EF model
            var media = MapReqToMedia(req);
            media.Name = nameToUse ?? media.Name;
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
                // When account uploads media, record usage so it appears in AccountMedia (scoped to account + site).
                // Only add a single row for the requested site; do not fall back to first site, so we never create links for multiple sites.
                if (req.AccountId.HasValue && req.SiteId.HasValue)
                    await _mediaStorage.AddAccountMediaUsageAsync(req.AccountId.Value, req.SiteId.Value, media.Id, cancelToken).ConfigureAwait(false);
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

        /// <param name="accountId">When set, delete only from this account's library. When null, global delete (soft-delete media).</param>
        /// <param name="siteId">When set with accountId, remove only this site's link so other sites keep the media.</param>
        public async Task<IApiResponse<bool>> DeleteMediaAsync(int mediaId, int? accountId, int? siteId, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var result = await _mediaStorage.DeleteMediaAsync(mediaId, accountId, siteId, cancelToken);
            response.Data = result;

            return response;
        }

        /// <summary>Record that an account/site uses a media item (idempotent).</summary>
        public async Task<IApiResponse<bool>> UseMediaAsync(int mediaId, UseMediaReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<bool>();

            var media = await _mediaStorage.GetMediaAsync(mediaId, cancelToken);
            if (media == null)
                return CreateResponse(response, StatusCode.ItemNotFound);

            var siteId = req.SiteId ?? await _mediaStorage.GetFirstSiteIdForAccountAsync(req.AccountId, cancelToken).ConfigureAwait(false);
            if (siteId.HasValue)
                await _mediaStorage.AddAccountMediaUsageAsync(req.AccountId, siteId.Value, mediaId, cancelToken).ConfigureAwait(false);
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

        /// <summary>Gets the file name from a URL (last path segment, without query or hash).</summary>
        private static string? GetFileNameFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var withoutQuery = url.Split('?')[0].Split('#')[0];
                var segments = withoutQuery.Split('/', '\\');
                var last = segments.Length > 0 ? segments[^1].Trim() : null;
                return !string.IsNullOrEmpty(last) ? last : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>True if the name looks like a storage-generated filename (e.g. Guid + extension).</summary>
        private static bool LooksLikeStorageFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            var withoutExt = System.IO.Path.GetFileNameWithoutExtension(name);
            // 32 hex chars (Guid without dashes) or very short random-looking name
            return withoutExt.Length == 32 && withoutExt.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
        }

        /// <summary>Download external media URLs and save files to our storage, then update media records.</summary>
        public async Task<IApiResponse<DownloadAndSaveMediaRes>> DownloadAndSaveToStorageAsync(DownloadAndSaveMediaReq req, CancellationToken cancelToken)
        {
            var response = new ApiResponse<DownloadAndSaveMediaRes>
            {
                Data = new DownloadAndSaveMediaRes()
            };
            var res = response.Data!;
            var userId = AuthUser?.Id;

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = TimeSpan.FromSeconds(60);
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("George-ShopManager/1.0");

            foreach (var mediaId in req.MediaIds.Distinct())
            {
                res.Processed++;
                try
                {
                    var media = await _mediaStorage.GetMediaAsync(mediaId, cancelToken);
                    if (media == null)
                    {
                        res.Failed++;
                        res.Errors.Add(new DownloadAndSaveError { MediaId = mediaId, Message = "Media not found." });
                        continue;
                    }

                    var url = media.Url?.Trim();
                    if (string.IsNullOrEmpty(url) || (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
                    {
                        res.Skipped++;
                        continue;
                    }

                    using var httpResponse = await httpClient.GetAsync(url, cancelToken);
                    httpResponse.EnsureSuccessStatusCode();
                    var bytes = await httpResponse.Content.ReadAsByteArrayAsync(cancelToken);
                    if (bytes.Length == 0)
                    {
                        res.Failed++;
                        res.Errors.Add(new DownloadAndSaveError { MediaId = mediaId, Message = "Downloaded file is empty." });
                        continue;
                    }

                    var contentType = httpResponse.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
                    var extension = GetExtensionFromContentType(contentType) ?? GetExtensionFromUrl(url) ?? ".jpg";
                    var fileName = "image" + extension;

                    var formFile = CreateFormFileFromBytes(bytes, fileName, contentType);
                    var path = FileHelper.GetTempFolderPath();
                    var uploadResult = await _fileStorage.UploadFileAsync(formFile, path, cancelToken);

                    if (!uploadResult.IsSuccessful || string.IsNullOrEmpty(uploadResult.FilePath))
                    {
                        res.Failed++;
                        res.Errors.Add(new DownloadAndSaveError { MediaId = mediaId, Message = uploadResult.Exception?.Message ?? "Upload failed." });
                        continue;
                    }

                    var newUrl = FileHelper.GetFileExternalPath(uploadResult.FilePath);
                    var updated = await _mediaStorage.UpdateMediaUrlAndSizeAsync(mediaId, newUrl, bytes.LongLength, userId, cancelToken);
                    if (updated)
                        res.Saved++;
                    else
                    {
                        res.Failed++;
                        res.Errors.Add(new DownloadAndSaveError { MediaId = mediaId, Message = "Failed to update media record." });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "DownloadAndSaveToStorage failed for media {MediaId}", mediaId);
                    res.Failed++;
                    res.Errors.Add(new DownloadAndSaveError { MediaId = mediaId, Message = ex.Message });
                }
            }

            return response;
        }

        private static string? GetExtensionFromContentType(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType)) return null;
            return contentType.ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/webp" => ".webp",
                "image/svg+xml" => ".svg",
                _ => null
            };
        }

        private static string? GetExtensionFromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            try
            {
                var path = url.Split('?')[0].Split('#')[0];
                var ext = System.IO.Path.GetExtension(path);
                return !string.IsNullOrEmpty(ext) ? ext : null;
            }
            catch { return null; }
        }

        private static IFormFile CreateFormFileFromBytes(byte[] bytes, string fileName, string contentType)
        {
            var stream = new MemoryStream(bytes);
            var formFile = new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
            return formFile;
        }
    }
}

