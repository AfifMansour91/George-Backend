namespace George.Api.Core;

/// <summary>Configuration for machine-to-machine access via <see cref="AutomationApiKeyAuthenticationHandler"/>.</summary>
public class AutomationApiKeyOptions
{
    /// <summary>Shared secret. If null or empty, automation key auth is disabled (only JWT applies).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Existing user id (e.g. system Admin) used for audit fields and <see cref="GeorgeControllerBase.TokenUserId"/>.</summary>
    public int ActAsUserId { get; set; }

    /// <summary>When true, <see cref="GeorgeControllerBase.TokenIsMaster"/> is true for automation calls.</summary>
    public bool ActAsMaster { get; set; } = true;
}
