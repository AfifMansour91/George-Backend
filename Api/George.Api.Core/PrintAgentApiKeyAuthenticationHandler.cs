using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Api.Core;

/// <summary>
/// Authenticates the local print agent via header X-Print-Agent-Key when it matches the configured key.
/// Use in appsettings: "PrintAgent": { "ApiKey": "your-secret-key" }. Agent sends header instead of Bearer.
/// </summary>
public class PrintAgentApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "PrintAgentApiKey";
    public const string HeaderName = "X-Print-Agent-Key";

    private readonly string? _configuredKey;

    public PrintAgentApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IOptions<PrintAgentApiKeyOptions> agentOptions)
        : base(options, logger, encoder)
    {
        _configuredKey = agentOptions.Value?.ApiKey?.Trim();
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrEmpty(_configuredKey))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (!Request.Headers.TryGetValue(HeaderName, out var keyHeader) || string.IsNullOrWhiteSpace(keyHeader))
            return Task.FromResult(AuthenticateResult.NoResult());

        var key = keyHeader.ToString().Trim();
        if (key != _configuredKey)
            return Task.FromResult(AuthenticateResult.Fail("Invalid print agent key."));

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.Name, "PrintAgent"));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
