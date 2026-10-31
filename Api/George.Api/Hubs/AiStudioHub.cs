using George.Api.Core;
using George.Common;
using George.Data;
using George.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace George.Api.Hubs;

/// <summary>
/// Progress of AI Product Studio jobs. A page calls <see cref="JoinAccount"/> and receives <c>AiJobUpdated</c> (an AiJobRes)
/// every time a job changes stage: queued → identify → preview_ready → final → done/failed. Mirrors <see cref="ScaleHub"/> gating:
/// a user may join their own account; a master/admin may join any.
/// </summary>
[Authorize]
public class AiStudioHub : Hub
{
    public const string JobUpdatedEvent = "AiJobUpdated";

    private readonly UserStorage _userStorage;
    private readonly ILogger<AiStudioHub> _logger;

    public AiStudioHub(UserStorage userStorage, ILogger<AiStudioHub> logger)
    {
        _userStorage = userStorage;
        _logger = logger;
    }

    public static string AccountGroup(int accountId) => $"aistudio:account:{accountId}";

    public async Task JoinAccount(int accountId)
    {
        var userId = ResolveUserId();
        if (userId <= 0 || accountId <= 0)
            return;

        var user = await _userStorage.GetThinUserAsync(userId).ConfigureAwait(false);
        var unrestricted = ResolveIsMaster() || user?.RoleId == (int)UserRole.Admin;
        if (!unrestricted && user?.AccountId != accountId)
        {
            _logger.LogWarning("AiStudio JoinAccount denied: user {UserId} asked for account {AccountId}", userId, accountId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, AccountGroup(accountId)).ConfigureAwait(false);
    }

    public Task LeaveAccount(int accountId) => Groups.RemoveFromGroupAsync(Context.ConnectionId, AccountGroup(accountId));

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
