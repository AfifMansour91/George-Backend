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

        public async Task<bool> DeleteMediaAsync(int mediaId, CancellationToken cancelToken)
        {
            var media = await _dbContext.Media
                .FirstOrDefaultAsync(m => m.Id == mediaId && !m.IsDeleted, cancelToken);

            if (media == null) return false;

            media.IsDeleted = true;
            media.UpdatedDate = DateTime.UtcNow;
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

