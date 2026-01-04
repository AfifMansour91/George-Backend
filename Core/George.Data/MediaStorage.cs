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
                .Include(m => m.Account)
                .Include(m => m.BusinessType)
                .Include(m => m.Type)
                .Include(m => m.Categories)
                .Include(m => m.Tags)
                .AsNoTracking();

            // Apply filters
            if (filter != null)
            {
                if (filter.AccountId.HasValue)
                {
                    query = query.Where(m => m.AccountId == filter.AccountId.Value);
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

            query = query.Skip(paging.Skip).Take(paging.Take);

            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Medium?> GetMediaAsync(int mediaId, CancellationToken cancelToken)
        {
            return await _dbContext.Media
                .Include(m => m.Account)
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

            // Add tags if provided
            if (tags != null && tags.Any())
            {
                foreach (var tagName in tags)
                {
                    var tag = await _dbContext.Tags
                        .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == media.AccountId, cancelToken);
                    
                    if (tag == null)
                    {
                        tag = new Tag
                        {
                            Name = tagName,
                            AccountId = media.AccountId,
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
            CancellationToken cancelToken)
        {
            var dbMedia = await _dbContext.Media
                .Include(m => m.Categories)
                .Include(m => m.Tags)
                .FirstOrDefaultAsync(m => m.Id == updated.Id && !m.IsDeleted, cancelToken);

            if (dbMedia == null) return null;

            // Update basic properties
            dbMedia.Url = updated.Url;
            dbMedia.Name = updated.Name;
            dbMedia.TypeId = updated.TypeId;
            dbMedia.BusinessTypeId = updated.BusinessTypeId;
            dbMedia.FileSize = updated.FileSize;
            dbMedia.UsageCount = updated.UsageCount;
            dbMedia.AccountId = updated.AccountId;
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

            // Update tags
            if (tags != null)
            {
                dbMedia.Tags.Clear();
                if (tags.Any())
                {
                    foreach (var tagName in tags)
                    {
                        var tag = await _dbContext.Tags
                            .FirstOrDefaultAsync(t => t.Name == tagName && t.AccountId == dbMedia.AccountId, cancelToken);
                        
                        if (tag == null)
                        {
                            tag = new Tag
                            {
                                Name = tagName,
                                AccountId = dbMedia.AccountId,
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

