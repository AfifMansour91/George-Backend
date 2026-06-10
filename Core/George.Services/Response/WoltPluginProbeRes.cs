namespace George.Services.Response;

/// <summary>Result of probing <c>GET {WooCommerceUrl}/wp-json/ed/v1/wolt</c>.</summary>
public class WoltPluginProbeRes
{
    /// <summary>HTTP 200 and JSON body parsed successfully.</summary>
    public bool EndpointReachable { get; set; }
    /// <summary>Value of <c>wolt</c> in the response when reachable.</summary>
    public bool PluginActive { get; set; }
    public string? ErrorMessage { get; set; }
}
