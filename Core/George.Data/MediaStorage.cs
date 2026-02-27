using George.Common;
using George.Common.Request;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data
{
    public class MediaStorage : StorageBase
    {
        public MediaStorage(GeorgeDBContext dbContext, ILogger<MediaStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Medium>> GetMediaAsync(
            MediaFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken)
        {
            var res = new DataListResult<Medium>();

            var query = _dbContext.Media
                .Include(m => m.BusinessType)
                .Include(m => m.Type)
                .Include(m => m.Categories)
                .Include(m => m.Tags)
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.GlobalOnly == true)
                {
                    // Global media: only media not used by any account (super-admin pool). Exclude all account media.
                    var usedMediaIds = await _dbContext.AccountMedia
                        .Select(am => am.MediaId)
                        .Distinct()
                        .ToListAsync(cancelToken)
                        .ConfigureAwait(false);
                    if (usedMediaIds.Count > 0)
                        query = query.Where(m => !usedMediaIds.Contains(m.Id));
                }
                else if (filter.AccountId.HasValue)
                {
                    var aid = filter.AccountId.Value;
                    // Media this account uses (AccountMedia only; Media has no AccountId)
                    query = query.Where(m =>
                        _dbContext.AccountMedia.Any(am => am.AccountId == aid && am.MediaId == m.Id));
                }

                if (filter.BusinessTypeId.HasValue)
                {
                    query = query.Where(m => m.BusinessTypeId == filter.BusinessTypeId.Value);
                }

                if (filter.CategoryId.HasValue)
                {
                    query = query.Where(m => m.Categories.Any(c => c.Id == filter.CategoryId.Value));
                }

                if (filter.Type.HasValue())
                {
                    query = query.Where(m => m.Type != null && m.Type.Name == filter.Type);
                }

                if (filter.Search?.SearchTerm.HasValue() == true)
                {
                    var term = filter.Search.SearchTerm!.Trim();
                    query = query.Where(m => m.Name.Contains(term) || m.Url.Contains(term));
                }
            }

            // Only get non-deleted media
            query = query.Where(m => !m.IsDeleted);

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            query = query.OrderByDescending(m => m.CreationTime);

            //query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Medium?> GetMediaAsync(int mediaId, CancellationToken cancelToken)
        {
            return await _dbContext.Media
                .Include(m => m.BusinessType)
                .Include(m => m.Type)
                .Include(m => m.Categories)
                .Include(m => m.Tags)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == mediaId && !m.IsDeleted, cancelToken);
        }

        public async Task<Medium> CreateMediaAsync(
            Medium media,
            List<int>? categoryIds,
            List<string>? tags,
            int? accountIdForTags,
            CancellationToken cancelToken)
        {
            _dbContext.Media.Add(media);

            // Add categories if provided
            if (categoryIds != null && categoryIds.Any())
            {
                var categories = await _dbContext.Categories
                    .Where(c => categoryIds.Contains(c.Id))
                    .ToListAsync(cancelToken);
                
                foreach (var category in categories)
                {
                    media.Categories.Add(category);
                }
            }

            // Add tags if provided (use accountIdForTags for per-account tag lookup)
            if (tags != null && tags.Any() && accountIdForTags.HasValue)
            {
                foreach (var tagName in tags)
                {
                    var tag = await _dbContext.Tags
                        .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == accountIdForTags.Value, cancelToken);
                    
                    if (tag == null)
                    {
                        tag = new Tag
                        {
                            Name = tagName,
                            AccountId = accountIdForTags.Value,
                            CreationTime = DateTime.UtcNow
                        };
                        _dbContext.Tags.Add(tag);
                    }
                    media.Tags.Add(tag);
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return media;
        }

        public async Task<Medium?> UpdateMediaAsync(
            Medium updated,
            List<int>? categoryIds,
            List<string>? tags,
            int? accountIdForTags,
            CancellationToken cancelToken)
        {
            var dbMedia = await _dbContext.Media
                .Include(m => m.Categories)
                .Include(m => m.Tags)
                .FirstOrDefaultAsync(m => m.Id == updated.Id && !m.IsDeleted, cancelToken);

            if (dbMedia == null) return null;

            // Update basic properties (no AccountId on Media)
            dbMedia.Url = updated.Url;
            dbMedia.Name = updated.Name;
            dbMedia.TypeId = updated.TypeId;
            dbMedia.BusinessTypeId = updated.BusinessTypeId;
            dbMedia.FileSize = updated.FileSize;
            dbMedia.UsageCount = updated.UsageCount;
            dbMedia.UpdatedDate = DateTime.UtcNow;
            dbMedia.UpdateUserId = updated.UpdateUserId;

            // Update categories
            if (categoryIds != null)
            {
                dbMedia.Categories.Clear();
                if (categoryIds.Any())
                {
                    var categories = await _dbContext.Categories
                        .Where(c => categoryIds.Contains(c.Id))
                        .ToListAsync(cancelToken);
                    
                    foreach (var category in categories)
                    {
                        dbMedia.Categories.Add(category);
                    }
                }
            }

            // Update tags (use accountIdForTags for per-account tag lookup)
            if (tags != null)
            {
                dbMedia.Tags.Clear();
                if (tags.Any() && accountIdForTags.HasValue)
                {
                    foreach (var tagName in tags)
                    {
                        var tag = await _dbContext.Tags
                            .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == accountIdForTags.Value, cancelToken);
                        
                        if (tag == null)
                        {
                            tag = new Tag
                            {
                                Name = tagName,
                                AccountId = accountIdForTags.Value,
                                CreationTime = DateTime.UtcNow
                            };
                            _dbContext.Tags.Add(tag);
                        }
                        dbMedia.Tags.Add(tag);
                    }
                }
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbMedia;
        }

        /// <summary>Updates only Url and FileSize for a media item (e.g. after downloading external URL to our storage). Also updates ProductImage and TemplateProductImage rows that reference this media so products keep showing the correct URL.</summary>
        public async Task<bool> UpdateMediaUrlAndSizeAsync(int mediaId, string url, long? fileSize, int? updateUserId, CancellationToken cancelToken)
        {
            var media = await _dbContext.Media
                .FirstOrDefaultAsync(m => m.Id == mediaId && !m.IsDeleted, cancelToken);
            if (media == null) return false;
            media.Url = url;
            media.FileSize = fileSize;
            media.UpdatedDate = DateTime.UtcNow;
            media.UpdateUserId = updateUserId;

            // Update ProductImage and TemplateProductImage URLs in place (Id is PK so Url can be updated)
            var productImages = await _dbContext.ProductImages
                .Where(pi => pi.MediaId == mediaId)
                .ToListAsync(cancelToken);
            foreach (var pi in productImages)
                pi.Url = url;

            var templateProductImages = await _dbContext.TemplateProductImages
                .Where(tpi => tpi.MediaId == mediaId)
                .ToListAsync(cancelToken);
            foreach (var tpi in templateProductImages)
                tpi.Url = url;

            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>
        /// Delete media from the library. When accountId is set (account admin deleting from account library),
        /// only removes the image from that account's products (ProductImage). When accountId is null (global delete),
        /// only removes from template products (TemplateProductImage) and soft-deletes the media.
        /// </summary>
        public async Task<bool> DeleteMediaAsync(int mediaId, int? accountId, CancellationToken cancelToken)
        {
            var media = await _dbContext.Media
                .FirstOrDefaultAsync(m => m.Id == mediaId && !m.IsDeleted, cancelToken);

            if (media == null) return false;

            if (accountId.HasValue)
            {
                // Account admin delete: remove image from this account's products only (ProductImage), and unlink from account library (AccountMedia)
                var accountProductIds = await _dbContext.Products
                    .Where(p => p.AccountId == accountId.Value)
                    .Select(p => p.Id)
                    .ToListAsync(cancelToken);
                var productImagesToRemove = await _dbContext.ProductImages
                    .Where(pi => pi.MediaId == mediaId && accountProductIds.Contains(pi.ProductId))
                    .ToListAsync(cancelToken);
                _dbContext.ProductImages.RemoveRange(productImagesToRemove);

                var accountMedia = await _dbContext.AccountMedia
                    .FirstOrDefaultAsync(am => am.AccountId == accountId.Value && am.MediaId == mediaId, cancelToken);
                if (accountMedia != null)
                    _dbContext.AccountMedia.Remove(accountMedia);

                // Do not set Media.IsDeleted - media may still be used globally or by other accounts
            }
            else
            {
                // Global delete: remove from template products (TemplateProductImage) and soft-delete the media
                var templateProductImagesToRemove = await _dbContext.TemplateProductImages
                    .Where(tpi => tpi.MediaId == mediaId)
                    .ToListAsync(cancelToken);
                _dbContext.TemplateProductImages.RemoveRange(templateProductImagesToRemove);

                media.IsDeleted = true;
                media.UpdatedDate = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        /// <summary>Record that an account uses a media item. Idempotent.</summary>
        public async Task AddAccountMediaUsageAsync(int accountId, int mediaId, CancellationToken cancelToken)
        {
            var exists = await _dbContext.AccountMedia
                .AnyAsync(am => am.AccountId == accountId && am.MediaId == mediaId, cancelToken)
                .ConfigureAwait(false);
            if (exists) return;

            _dbContext.AccountMedia.Add(new AccountMedia
            {
                AccountId = accountId,
                MediaId = mediaId,
                CreationTime = DateTime.UtcNow
            });
            await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
        }

        /// <summary>Returns URL -> MediaId for media that belong to the account (via AccountMedia) and have one of the given URLs. Used when linking product images to existing account media.</summary>
        public async Task<Dictionary<string, int>> GetMediaIdsByUrlsForAccountAsync(int accountId, List<string> urls, CancellationToken cancelToken)
        {
            if (urls == null || !urls.Any()) return new Dictionary<string, int>();

            var normalized = urls.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!.Trim()).Distinct().ToList();
            if (normalized.Count == 0) return new Dictionary<string, int>();

            var list = await _dbContext.Media
                .Where(m => !m.IsDeleted && normalized.Contains(m.Url)
                    && _dbContext.AccountMedia.Any(am => am.AccountId == accountId && am.MediaId == m.Id))
                .Select(m => new { m.Url, m.Id })
                .ToListAsync(cancelToken);

            return list.ToDictionary(x => x.Url, x => x.Id);
        }

        /// <summary>Returns MediaId for the given URL in the global media pool: existing Media with that URL, or a new Media record (no AccountMedia). Used when importing global/template products with external image URLs.</summary>
        public async Task<int?> GetOrCreateMediaByUrlAsync(string url, int? creationUserId, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();

            var existing = await _dbContext.Media
                .Where(m => !m.IsDeleted && m.Url == trimmed)
                .Select(m => m.Id)
                .FirstOrDefaultAsync(cancelToken);
            if (existing != 0) return existing;

            var typeId = await GetOrCreateMediaTypeAsync("image", cancelToken);
            var name = GetFileNameFromUrl(trimmed) ?? "image";
            var media = new Medium
            {
                Url = trimmed,
                Name = name,
                TypeId = typeId,
                CreationUserId = creationUserId,
                CreationTime = DateTime.UtcNow,
                IsDeleted = false
            };
            _dbContext.Media.Add(media);
            await _dbContext.SaveChangesAsync(cancelToken);
            return media.Id;
        }

        /// <summary>Returns MediaId for the given URL in this account: existing account media with that URL, or a new Media record (and AccountMedia) for external URLs. Used when importing products with external image URLs.</summary>
        public async Task<int?> GetOrCreateMediaByUrlForAccountAsync(int accountId, string url, int? creationUserId, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();

            var existing = await GetMediaIdsByUrlsForAccountAsync(accountId, new List<string> { trimmed }, cancelToken);
            if (existing.TryGetValue(trimmed, out var existingId)) return existingId;

            var typeId = await GetOrCreateMediaTypeAsync("image", cancelToken);
            var name = GetFileNameFromUrl(trimmed) ?? "image";
            var media = new Medium
            {
                Url = trimmed,
                Name = name,
                TypeId = typeId,
                CreationUserId = creationUserId,
                CreationTime = DateTime.UtcNow,
                IsDeleted = false
            };
            _dbContext.Media.Add(media);
            await _dbContext.SaveChangesAsync(cancelToken);
            await AddAccountMediaUsageAsync(accountId, media.Id, cancelToken);
            return media.Id;
        }

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

        // Helper method to get or create MediaType
        public async Task<int?> GetOrCreateMediaTypeAsync(string typeName, CancellationToken cancelToken)
        {
            if (string.IsNullOrWhiteSpace(typeName)) return null;

            var mediaType = await _dbContext.MediaTypes
                .FirstOrDefaultAsync(mt => mt.Name == typeName && !mt.IsDeleted, cancelToken);

            if (mediaType == null)
            {
                mediaType = new MediaType
                {
                    Name = typeName,
                    IsDeleted = false
                };
                _dbContext.MediaTypes.Add(mediaType);
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return mediaType.Id;
        }
    }
}

