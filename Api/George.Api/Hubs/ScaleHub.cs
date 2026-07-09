using George.Api.Core;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace George.Api.Hubs;

/// <summary>
/// Consumer-facing hub for live scale weight. The picking page (iPad or Windows) calls
/// <see cref="JoinSite"/> and then receives <c>ScaleWeightChanged</c> events for that branch.
/// Weight is produced by the branch ScaleAgent (POST /Scale/Reading) and broadcast to the site group.
/// Mirrors <see cref="OrdersHub"/> site-access gating.
/// </summary>
[Authorize]
public class ScaleHub : Hub
{
    private readonly SiteAccessService _siteAccess;
    private readonly ILogger<ScaleHub> _logger;

    public ScaleHub(SiteAccessService siteAccess, ILogger<ScaleHub> logger)
    {
        _siteAccess = siteAccess;
        _logger = logger;
    }

    public static string SiteGroup(int siteId) => $"site:{siteId}";

    public async Task JoinSite(int siteId, int? accountId = null)
    {
        if (!SysConfig.Data.ScaleRealtimeEnabled)
            return;

        if (!await CanJoinSiteAsync(siteId, accountId).ConfigureAwait(false))
        {
            _logger.LogWarning("Scale JoinSite denied for connection {ConnectionId}, site {SiteId}", Context.ConnectionId, siteId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, SiteGroup(siteId)).ConfigureAwait(false);
    }

    public async Task LeaveSite(int siteId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, SiteGroup(siteId)).ConfigureAwait(false);
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
