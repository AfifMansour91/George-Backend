using System.Security.Claims;
using System.Text.Encodings.Web;
using George.Common;
using George.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Api.Core;

/// <summary>
/// Machine-to-machine authentication via header <c>X-Automation-Api-Key</c> matching <see cref="AutomationApiKeyOptions.ApiKey"/>.
/// Sets JWT-equivalent claims (<see cref="CustomClaimType.UserId"/>, <see cref="CustomClaimType.IsMaster"/>) using <see cref="AutomationApiKeyOptions.ActAsUserId"/>.
/// Configure in appsettings: <c>Automation:ApiKey</c>, <c>Automation:ActAsUserId</c>, <c>Automation:ActAsMaster</c>.
/// </summary>
public class AutomationApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "AutomationApiKey";
    public const string HeaderName = "X-Automation-Api-Key";

    private readonly string? _configuredKey;
    private readonly int _actAsUserId;
    private readonly bool _actAsMaster;

    public AutomationApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<AutomationApiKeyOptions> automationOptions)
        : base(options, logger, encoder)
    {
        var o = automationOptions.Value;
        _configuredKey = o?.ApiKey?.Trim();
        _actAsUserId = o?.ActAsUserId ?? AuthHelper.INVALID_ID;
        _actAsMaster = o?.ActAsMaster ?? true;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrEmpty(_configuredKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(HeaderName, out var keyHeader) || string.IsNullOrWhiteSpace(keyHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var key = keyHeader.ToString().Trim();
        if (key != _configuredKey)
            return Task.FromResult(AuthenticateResult.Fail("Invalid automation API key."));

        if (_actAsUserId <= 0)
            return Task.FromResult(AuthenticateResult.Fail("Automation ActAsUserId is not configured."));

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(CustomClaimType.Authorized, "1"));
        identity.AddClaim(new Claim(CustomClaimType.UserId, _actAsUserId.ToString()));
        if (_actAsMaster)
            identity.AddClaim(new Claim(CustomClaimType.IsMaster, "true"));
        identity.AddClaim(new Claim(ClaimTypes.Name, "Automation"));

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
