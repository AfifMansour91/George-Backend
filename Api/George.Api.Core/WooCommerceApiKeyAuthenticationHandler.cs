using System.Security.Claims;
using System.Text.Encodings.Web;
using George.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace George.Api.Core;

/// <summary>
/// Authenticates external integrations (e.g. WooCommerce plugin) via X-Api-Key header. Key must match Site.InternalApiKey.
/// Sets claims: SiteId, AccountId so endpoints can scope to the site.
/// </summary>
public class WooCommerceApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "WooCommerceApiKey";
    public const string HeaderName = "X-Api-Key";
    public const string ClaimSiteId = "WooCommerceSiteId";
    public const string ClaimAccountId = "WooCommerceAccountId";

    public WooCommerceApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // Accept X-Api-Key or Authorization: Bearer <key>
        string? key = null;
        if (Request.Headers.TryGetValue(HeaderName, out var keyHeader) && !string.IsNullOrWhiteSpace(keyHeader))
            key = keyHeader.ToString().Trim();
        if (string.IsNullOrEmpty(key) && Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var val = authHeader.ToString();
            if (val.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                key = val.Substring(7).Trim();
        }

        if (string.IsNullOrEmpty(key))
            return AuthenticateResult.NoResult();

        var siteStorage = Context.RequestServices.GetRequiredService<SiteStorage>();
        var site = await siteStorage.GetSiteByInternalApiKeyAsync(key, Context.RequestAborted);
        if (site == null)
            return AuthenticateResult.Fail("Invalid or unknown API key.");

        var identity = new ClaimsIdentity(SchemeName);
        identity.AddClaim(new Claim(ClaimTypes.Name, "WooCommerce"));
        identity.AddClaim(new Claim(ClaimSiteId, site.Id.ToString()));
        identity.AddClaim(new Claim(ClaimAccountId, site.AccountId.ToString()));
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
