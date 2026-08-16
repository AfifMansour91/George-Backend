using AutoMapper;
using George.Common;
using George.Data;
using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Services;

/// <summary>Resolves which site IDs the current (or specified) user may access — shared by dashboard, SignalR hub, etc.</summary>
public class SiteAccessService : ServiceBase
{
    private readonly UserStorage _userStorage;
    private readonly SiteStorage _siteStorage;

    public SiteAccessService(
        ILogger<SiteAccessService> logger,
        IMapper mapper,
        CacheManager cache,
        UserStorage userStorage,
        SiteStorage siteStorage)
        : base(logger, mapper, cache)
    {
        _userStorage = userStorage;
        _siteStorage = siteStorage;
    }

    /// <summary>
    /// Site-listing scope for the user: null = unrestricted (master/admin may list all sites);
    /// otherwise the allowed site ids — empty when the user has no account (fail closed).
    /// </summary>
    public async Task<HashSet<int>?> GetSiteScopeAsync(
        int userId,
        bool isMaster,
        CancellationToken cancelToken = default)
    {
        if (userId <= 0)
            return new HashSet<int>();

        var user = await _userStorage.GetUserAsync(userId, cancelToken).ConfigureAwait(false);
        if (user == null)
            return new HashSet<int>();

        if (isMaster || user.RoleId == (int)UserRole.Admin)
            return null;

        if (user.AccountId is not > 0)
            return new HashSet<int>();

        var accountSites = await _siteStorage
            .GetSitesByAccountAsync(user.AccountId.Value, cancelToken)
            .ConfigureAwait(false);
        var allowed = accountSites.Select(s => s.Id).ToHashSet();

        if (user.RoleId == (int)UserRole.SiteAdmin)
        {
            var assignedSiteIds = user.Site is { Count: > 0 }
                ? user.Site.Select(s => s.Id).ToHashSet()
                : (await _siteStorage.GetSiteIdsForUserAsync(user.Id, cancelToken).ConfigureAwait(false)).ToHashSet();
            if (assignedSiteIds.Count > 0)
                allowed.IntersectWith(assignedSiteIds);
        }

        return allowed;
    }

    public async Task<HashSet<int>> GetAccessibleSiteIdsAsync(
        int userId,
        bool isMaster,
        int? accountIdOverride,
        CancellationToken cancelToken = default)
    {
        if (userId <= 0)
            return new HashSet<int>();

        var user = await _userStorage.GetUserAsync(userId, cancelToken).ConfigureAwait(false);
        if (user == null)
            return new HashSet<int>();

        var roleId = user.RoleId;
        var master = isMaster || roleId == (int)UserRole.Admin;

        int? effectiveAccountId = user.AccountId is > 0 ? user.AccountId : null;
        if (master && accountIdOverride is > 0)
            effectiveAccountId = accountIdOverride;

        List<Site> accountSites;
        if (effectiveAccountId is > 0)
        {
            accountSites = await _siteStorage
                .GetSitesByAccountAsync(effectiveAccountId.Value, cancelToken)
                .ConfigureAwait(false);
        }
        else if (master)
        {
            accountSites = (await _siteStorage
                .GetSitesAsync(new SiteFilter(), new PagingExDto(10_000), cancelToken)
                .ConfigureAwait(false)).Items;
        }
        else
        {
            return new HashSet<int>();
        }

        var allowed = accountSites.Select(s => s.Id).ToHashSet();

        if (roleId == (int)UserRole.SiteAdmin)
        {
            var assignedSiteIds = user.Site is { Count: > 0 }
                ? user.Site.Select(s => s.Id).ToHashSet()
                : (await _siteStorage.GetSiteIdsForUserAsync(user.Id, cancelToken).ConfigureAwait(false)).ToHashSet();
            if (assignedSiteIds.Count > 0)
                allowed.IntersectWith(assignedSiteIds);
        }

        return allowed;
    }

    public async Task<bool> CanAccessSiteAsync(
        int userId,
        bool isMaster,
        int siteId,
        int? accountIdOverride,
        CancellationToken cancelToken = default)
    {
        if (siteId <= 0)
            return false;
        var allowed = await GetAccessibleSiteIdsAsync(userId, isMaster, accountIdOverride, cancelToken)
            .ConfigureAwait(false);
        return allowed.Contains(siteId);
    }
}
