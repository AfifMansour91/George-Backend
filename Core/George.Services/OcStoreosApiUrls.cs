namespace George.Services;

/// <summary>Builds oc-storeos REST base path from the WooCommerce site root URL (same as wc/v3 base host).</summary>
internal static class OcStoreosApiUrls
{
    /// <returns><c>{siteRoot}/wp-json/oc-storeos/v1</c> or null when the root URL is empty.</returns>
    internal static string? V1BaseFromWooCommerceRoot(string? wooCommerceStoreRootUrl)
    {
        if (string.IsNullOrWhiteSpace(wooCommerceStoreRootUrl)) return null;
        return $"{wooCommerceStoreRootUrl.Trim().TrimEnd('/')}/wp-json/oc-storeos/v1";
    }
}
