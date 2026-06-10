using George.Api.Core;
using George.Common;
using George.Data;
using George.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace George.Api.Hubs;

[Authorize]
public class OrdersHub : Hub
{
    private readonly SiteAccessService _siteAccess;
    private readonly RealtimeLogStorage _logStorage;
    private readonly ILogger<OrdersHub> _logger;

    public OrdersHub(
        SiteAccessService siteAccess,
        RealtimeLogStorage logStorage,
        ILogger<OrdersHub> logger)
    {
        _siteAccess = siteAccess;
        _logStorage = logStorage;
        _logger = logger;
    }

    public static string SiteGroup(int siteId) => $"site:{siteId}";

    public override async Task OnConnectedAsync()
    {
        await AppendHubLogAsync("Connected", null, null, null).ConfigureAwait(false);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await AppendHubLogAsync("Disconnected", null, null, exception?.Message).ConfigureAwait(false);
        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    public async Task JoinSite(int siteId, int? accountId = null)
    {
        if (!SysConfig.Data.OrdersRealtimeEnabled)
        {
            await AppendHubLogAsync("JoinSiteDenied", siteId, accountId, "OrdersRealtimeEnabled=false").ConfigureAwait(false);
            return;
        }

        if (!await CanJoinSiteAsync(siteId, accountId).ConfigureAwait(false))
        {
            _logger.LogWarning("JoinSite denied for connection {ConnectionId}, site {SiteId}", Context.ConnectionId, siteId);
            await AppendHubLogAsync("JoinSiteDenied", siteId, accountId, "access denied").ConfigureAwait(false);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SiteGroup(siteId)).ConfigureAwait(false);
        await AppendHubLogAsync("JoinSite", siteId, accountId, null).ConfigureAwait(false);
    }

    public async Task LeaveSite(int siteId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SiteGroup(siteId)).ConfigureAwait(false);
        await AppendHubLogAsync("LeaveSite", siteId, null, null).ConfigureAwait(false);
    }

    public async Task JoinSites(int[] siteIds, int? accountId = null)
    {
        if (!SysConfig.Data.OrdersRealtimeEnabled)
        {
            await AppendHubLogAsync("JoinSitesDenied", null, accountId, "OrdersRealtimeEnabled=false").ConfigureAwait(false);
            return;
        }

        if (siteIds == null || siteIds.Length == 0)
            return;

        var joined = 0;
        var distinct = siteIds.Where(id => id > 0).Distinct().ToArray();
        foreach (var siteId in distinct)
        {
            if (!await CanJoinSiteAsync(siteId, accountId).ConfigureAwait(false))
            {
                _logger.LogWarning("JoinSites denied for connection {ConnectionId}, site {SiteId}", Context.ConnectionId, siteId);
                continue;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, SiteGroup(siteId)).ConfigureAwait(false);
            joined++;
        }

        await AppendHubLogAsync("JoinSites", null, accountId, $"joined={joined}/{distinct.Length}").ConfigureAwait(false);
    }

    private async Task AppendHubLogAsync(
        string eventType,
        int? siteId,
        int? accountId,
        string? detail)
    {
        try
        {
            int? userId = ResolveUserId();
            if (userId <= 0) userId = null;
            await _logStorage
                .AppendHubLogAsync(
                    RealtimeHubNames.Orders,
                    eventType,
                    Context.ConnectionId,
                    userId,
                    siteId,
                    accountId,
                    RealtimeFeatures.NewOrder,
                    detail)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RealtimeHubLog write failed for {EventType}", eventType);
        }
    }

    private async Task<bool> CanJoinSiteAsync(int siteId, int? accountIdOverride)
    {
        var userId = ResolveUserId();
        if (userId <= 0)
            return false;

        return await _siteAccess
            .CanAccessSiteAsync(userId, ResolveIsMaster(), siteId, accountIdOverride)
            .ConfigureAwait(false);
    }

    private int ResolveUserId()
    {
        if (Globals.OverrideAuthentication)
            return Globals.OverrideUserId;

        return (Context.User?.FindFirst(CustomClaimType.UserId)?.Value).ToInt(AuthHelper.INVALID_ID);
    }

    private bool ResolveIsMaster()
    {
        if (Globals.OverrideAuthentication)
            return Globals.OverrideIsMaster;

        var raw = Context.User?.FindFirst(CustomClaimType.IsMaster)?.Value;
        return raw.HasValue() && raw!.ToBool(false);
    }
}
