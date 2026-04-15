namespace George.Services;

/// <summary>Builds oc-storeos REST base path from a storefront/site root URL (e.g. Site.WooCommerceOrderUpdateBaseUrl).</summary>
internal static class OcStoreosApiUrls
{
    /// <returns><c>{siteRoot}/wp-json/oc-storeos/v1</c> or null when the root URL is empty.</returns>
    internal static string? V1BaseFromWooCommerceRoot(string? wooCommerceStoreRootUrl)
    {
        if (string.IsNullOrWhiteSpace(wooCommerceStoreRootUrl)) return null;
        return $"{wooCommerceStoreRootUrl.Trim().TrimEnd('/')}/wp-json/oc-storeos/v1";
    }
}
