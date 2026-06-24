using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace George.Data;

/// <summary>
/// Persists + reads <see cref="IntegrationLog"/> rows — the system-wide record of what George sent to /
/// received from a store, across all entity kinds (order / product / category / …). Backs a future admin
/// "sync logs" screen. Writes must never break the operation they log, so callers wrap <see cref="AddAsync"/>
/// in try/catch.
/// </summary>
public class IntegrationLogStorage : StorageBase
{
    public IntegrationLogStorage(GeorgeDBContext dbContext, ILogger<IntegrationLogStorage> logger)
        : base(dbContext, logger)
    {
    }

    public async Task AddAsync(IntegrationLog log, CancellationToken cancelToken)
    {
        _dbContext.IntegrationLog.Add(log);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>Batch insert (one DB round-trip) — used by the background log writer.</summary>
    public async Task AddRangeAsync(IReadOnlyList<IntegrationLog> logs, CancellationToken cancelToken)
    {
        if (logs == null || logs.Count == 0) return;
        _dbContext.IntegrationLog.AddRange(logs);
        await _dbContext.SaveChangesAsync(cancelToken).ConfigureAwait(false);
    }

    /// <summary>All log rows for a specific entity (e.g. one order / product / category), newest first.</summary>
    public async Task<IReadOnlyList<IntegrationLog>> GetForEntityAsync(string entityType, int entityId, CancellationToken cancelToken)
    {
        return await _dbContext.IntegrationLog.AsNoTracking()
            .Where(l => l.EntityType == entityType && l.EntityId == entityId)
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.Id)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>Recent log rows for a site, optionally filtered by entity type, newest first (paged) — for the logs screen.</summary>
    public async Task<IReadOnlyList<IntegrationLog>> GetRecentForSiteAsync(int siteId, string? entityType, int skip, int take, CancellationToken cancelToken)
    {
        take = take <= 0 ? 50 : Math.Min(take, 500);
        var query = _dbContext.IntegrationLog.AsNoTracking().Where(l => l.SiteId == siteId);
        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var t = entityType.Trim();
            query = query.Where(l => l.EntityType == t);
        }
        return await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip(Math.Max(0, skip))
            .Take(take)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Paged + filtered log rows for the admin logs screen, newest first, plus the total matching count
    /// (so the screen can render pagination). All filters are optional and combine with AND; <paramref name="search"/>
    /// matches ExternalId / Operation / Url / Error (case-insensitive contains).
    /// </summary>
    public async Task<(IReadOnlyList<IntegrationLog> Items, int Total)> GetPagedForSiteAsync(
        int siteId,
        string? entityType,
        string? direction,
        string? level,
        bool? success,
        string? search,
        int skip,
        int take,
        CancellationToken cancelToken)
    {
        take = take <= 0 ? 50 : Math.Min(take, 500);
        skip = Math.Max(0, skip);

        var query = _dbContext.IntegrationLog.AsNoTracking().Where(l => l.SiteId == siteId);

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var t = entityType.Trim();
            query = query.Where(l => l.EntityType == t);
        }
        if (!string.IsNullOrWhiteSpace(direction))
        {
            var d = direction.Trim();
            query = query.Where(l => l.Direction == d);
        }
        if (!string.IsNullOrWhiteSpace(level))
        {
            var lvl = level.Trim();
            query = query.Where(l => l.Level == lvl);
        }
        if (success.HasValue)
        {
            var s = success.Value;
            query = query.Where(l => l.Success == s);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                (l.ExternalId != null && EF.Functions.Like(l.ExternalId, $"%{term}%"))
                || EF.Functions.Like(l.Operation, $"%{term}%")
                || (l.Url != null && EF.Functions.Like(l.Url, $"%{term}%"))
                || (l.Error != null && EF.Functions.Like(l.Error, $"%{term}%")));
        }

        var total = await query.CountAsync(cancelToken).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(l => l.CreatedAtUtc)
            .ThenByDescending(l => l.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancelToken)
            .ConfigureAwait(false);
        return (items, total);
    }
}
